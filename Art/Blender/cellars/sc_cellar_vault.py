"""CellarVault: B5 cellar vault kit (T2, PHASE1 4 / dungeon corner), the pieces of one barrel-vault cellar room.

Original storybook cellar in the Dark Cloud tradition rendered in stylised realism: chunky coursed brick walls and a rubble-stone
barrel vault with brick bands, 1-3 degrees off true, chamfered stones, a brick arch rib on engaged piers, limewashed end walls
with a wine-niche wall (3 x 3 arched niches, bottles lying in them), a stair entry, flagstone floor tiles, a coal chute with a
daylight hatch and a coal heap, oak larder shelves of glazed jars, an iron breath grate over an ember glow, hanging roots and
a cradle of three barrels. Contract: Reference/hollowmere/dungeon_corner/CONTRACT.md (piece names, extents in Unity terms).

Pieces (each a direct child mesh of root CellarVault, authored at its own pivot at the world origin, pieces overlap in the file):
  VaultSegment  x -3..3, z 0..2, y 0..4.6 (Unity), side walls + barrel vault, no floor
  ArchRib       transverse brick rib in the plane z = 0 with its two engaged piers
  Pillar        free-standing square stone pier, chamfered cap
  EndWall / NicheWall / StairEntry   wall bodies z -0.5..0 facing +Z (niche wall: 9 arched niches + 35 bottles; stair: doorway + 13 steps to y 2.6)
  FloorTile     x -1..1, z 0..2, irregular flagstones
  CoalChute     right wall (x +2.5), centred z 0: hatch (emissive daylight quad), wooden chute, coal heap
  LarderShelf   left wall (x -2.5), z 0..2, y 0..2.2: oak shelves, iron brackets, jars, crocks, bottles, a cheese
  BreathGrate   x -0.6..0.6, z -0.6..0.6: iron bars over the CV_Breath ember glow
  Roots         origin at the top on the vault crown (y 4.1), drops to about y 2.4
  Barrels       two lying on a cradle (axes along X) and one standing, origin on the floor

Authoring frame (this file): A = (x across, y along the vault, z up) = Unity (x, z, y). The finished meshes are rotated 180 degrees
about Z into Blender axes, so the FBX export (-Z forward, Y up) lands on Unity (x, y, z) = Blender (-x, z, -y) as the contract says.
Materials (T2: shared B2 cellar tileables + B1 oak/iron, metre UVs 2 m per tile, linear CORNER float colour attribute "wear":
R grime/soot/damp, G moss, B polish) are named CV_*; CV_Breath and CV_Daylight are the only emissive slots.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/cellars/sc_cellar_vault.py -- --out <scratch>
      [--views wide,mid,close | lineA,lineB,lineC,lineD] [--rres 1000] [--samples 24] [--no-render] [--room] [--export] [--export-dir <dir>]
      [--tris] [--blend]
  --room assembles the contract room (review renders only, never exported); the three capture cameras are Cap_Cellar_Wide/Mid/Close
  (Unity coordinates converted); the lineup views show the loose kit. --tris prints triangles per piece and the total.
Budget: kit under 60k tris, a piece under 12k.

--stair builds STAIR_PIECES (Q13 undercroft stair; not part of PIECES, CellarVault.fbx is unaffected) under the empty root CellarStair:
  StairFlight   8 steps (run 2.0, drop 1.6), sloped brick walls, 4 stepped vault rings on a mortar backing, brick arch band
  StairTurn     right-hand winder quarter turn, 4 winders round a newel, sail vault, tympanum over the exit arch
  StairDoor     home cellar door, flagstone landing with barrel vault, door wall, jambs, threshold, oak door open 105 deg, house floor
  with --export writes CellarStair.fbx to --export-dir; --stair --room assembles the chain (Unity yaw about +Y, instance() uses -yaw about
  Blender Z) with the StairEntry, proxy lights and cameras top/turn/bottom (--sl scales the proxy lights). Budget: piece under 8k, all under 20k.
  Status (2026-10-31): 14,132 tris (4,252 / 3,788 / 6,092), joins checked in 2 render passes (no gaps, treads continuous, chain ends at StairEntry's top step).

Status (2026-10-31, cloud): 3 review rounds (+ one bug-fix re-render), self-score 27 -> 28.5 -> 29/40 (calibrated rubric, target first + 5 = 32 not
reached: round 3 gained 0.5, so stopped). Final lines: silhouette 3.5, proportion 4, form 3.5, surface 3.5, colour 3.5, wear 3.5, set 4,
glass/metal/emissive 3.5. 36,460 tris (VaultSegment 5,372, ArchRib 1,100, Pillar 220, EndWall 750, NicheWall 8,700, StairEntry 2,876,
FloorTile 1,772, CoalChute 4,170, LarderShelf 4,824, BreathGrate 604, Roots 2,776, Barrels 3,296; two --no-render --tris runs agree).
Round 2 made the end walls limewashed with ragged windows where the render fell off (boolean cutters, chamfered rims) and the bottle glass olive
green; round 3 raised the lamps, made the tide line, salt bloom and lantern soot plumes readable. Sheet: Reference/hollowmere/dungeon_corner/SHEET_cellar_vault.jpg.
Next visible gains: a real lantern in the hooks (B11) to pool gold light and lift the wide/mid p90 (0.46/0.52 vs the 0.55-0.68 band; the room is
lit by two proxy lamps), pillar/rib stone with a moulded cap profile, glaze runs and crackle on the jars, brick pattern variation per course,
a chute with a visible coal slide, thicker moss in the tide zone. Wear (soot plumes at the lantern hooks, root stains under the crack) is only
written for the room-mode copies; the exported kit carries the generic tide line, salt bloom, vault smoke, tread polish.
"""
import sys, os, math, zlib
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'town'))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT
argv = FK.argv
VIEWS = FK.arg('--views', 'wide,mid,close' if '--room' in FK.argv else 'lineA,lineB,lineC,lineD').split(',')
RRES = int(FK.arg('--rres', '1000')); SAMPLES = int(FK.arg('--samples', '24'))
TILE = FK.TILE
SPRING, R_IN, R_OUT, MOD = 1.6, 2.5, 3.0, 2.0
R_ = lambda s: FK.rng('cv:' + s)

def Rx(d): return Matrix.Rotation(math.radians(d), 4, 'X')
def Ry(d): return Matrix.Rotation(math.radians(d), 4, 'Y')
def Rz(d): return Matrix.Rotation(math.radians(d), 4, 'Z')
def T(*v): return Matrix.Translation(Vector(v))

# ----------------------------------------------------------------------------- materials
def glass_mat():
    if 'CV_Glass' in FK.MATS:
        return FK.MATS['CV_Glass']
    m = bpy.data.materials.new('CV_Glass'); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (0.10, 0.15, 0.03, 1)      # old olive-green bottle glass
    b.inputs['Roughness'].default_value = 0.05
    b.inputs['IOR'].default_value = 1.45
    b.inputs['Transmission Weight'].default_value = 0.6
    FK.MATS['CV_Glass'] = m
    return m

def emissive(name, base, emit, strength):
    if name in FK.MATS:
        return FK.MATS[name]
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = m.node_tree.nodes['Principled BSDF']
    b.inputs['Base Color'].default_value = (*base, 1); b.inputs['Roughness'].default_value = 0.8
    b.inputs['Emission Color'].default_value = (*emit, 1); b.inputs['Emission Strength'].default_value = strength
    FK.MATS[name] = m
    return m

def flat(name, col, rough):
    return lambda: FK.flat_mat(name, col, rough)

EXTRA = {
    'brick': lambda: FK.tile_mat('CV_Brick', 'B2_cellar/old_brick', tint=(0.98, 1.04, 1.0), normal=1.2),
    'vault': lambda: FK.tile_mat('CV_VaultStone', 'B2_cellar/vault_stone', tint=(0.93, 0.91, 0.88), normal=1.3),
    'lime': lambda: FK.tile_mat('CV_Limewash', 'B2_cellar/limewash', tint=(1.06, 1.0, 0.94), normal=0.45),
    'flag': lambda: FK.tile_mat('CV_Flagstone', 'B2_cellar/flagstone', tint=(1.0, 0.95, 0.9), normal=1.2),
    'barrel': lambda: FK.tile_mat('CV_BarrelOak', 'B2_cellar/barrel_oak', tint=(1.1, 1.0, 0.9), normal=1.2),
    'soot': lambda: FK.tile_mat('CV_Soot', 'B2_cellar/soot_coal', tint=(1.1, 1.05, 1.0), rough_add=0.3, normal=1.3),
    'root': lambda: FK.tile_mat('CV_RootBark', 'B2_cellar/root_bark', tint=(2.0, 1.65, 1.25), normal=1.3),
    'oak': lambda: FK.tile_mat('CV_Oak', 'B1_town/oak_timber', tint=(0.85, 0.74, 0.62), normal=1.2),
    'iron': lambda: FK.tile_mat('CV_Iron', 'B1_town/iron', tint=(0.62, 0.58, 0.55)),
    'mortar': lambda: FK.tile_mat('CV_Mortar', 'B2_cellar/vault_stone', tint=(0.22, 0.19, 0.16), normal=0.6),
    'clay': lambda: FK.tile_mat('CV_Clay', 'B2_cellar/limewash', tint=(1.05, 0.58, 0.34), normal=1.0),
    'glass': glass_mat,
    'glaze': flat('CV_GlazeAmber', (0.26, 0.095, 0.022), 0.17),
    'glaze2': flat('CV_GlazeCream', (0.62, 0.46, 0.24), 0.25),
    'cheese': flat('CV_Cheese', (0.55, 0.30, 0.05), 0.55),
    'breath': lambda: emissive('CV_Breath', (0.25, 0.04, 0.012), (1.0, 0.30, 0.05), 2.2),
    'daylight': lambda: emissive('CV_Daylight', (0.7, 0.7, 0.68), (1.0, 0.97, 0.90), 6.0),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)
SMOOTH = {'CV_Glass', 'CV_GlazeAmber', 'CV_GlazeCream', 'CV_Clay', 'CV_Cheese', 'CV_RootBark', 'CV_Breath', 'CV_Daylight'}

# ----------------------------------------------------------------------------- geometry helpers
def box(pc, mat, size, c, rot=None, cham=0.02, **kw):
    kw.setdefault('uv_world', True)
    FK.add_box(pc, mat, size, Matrix.Translation(Vector(c)) @ (rot if rot is not None else Matrix.Identity(4)), cham=cham, **kw)

def merge_faces(pc, bm, uvl, keyfn):
    """Append bm (own UVMap layer uvl) into pc with the material key keyfn(face) per face."""
    vmap = {v: pc.bm.verts.new(v.co) for v in bm.verts}
    for f in bm.faces:
        try:
            nf = pc.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError:
            continue
        nf.material_index = pc.slot(keyfn(f))
        for l, nl in zip(f.loops, nf.loops):
            nl[pc.uv].uv = l[uvl].uv
    bm.free()

def lathe(pc, prof, xf, sides=12, mat='glaze', seed=0.0, keyfn=None, closed=False):
    """Surface of revolution about local z; prof = [(r, h)] bottom to top; keyfn(i, k) picks the key of the band i, side k."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = []
    for r, h in prof:
        rings.append([bm.verts.new((r * math.cos(2 * math.pi * k / sides), r * math.sin(2 * math.pi * k / sides), h))
                      for k in range(sides)] if r > 1e-6 else [bm.verts.new((0, 0, h))])
    n = len(prof) if closed else len(prof) - 1
    keys = {}
    for i in range(n):
        a, b = rings[i], rings[(i + 1) % len(prof)]
        for k in range(sides):
            k2 = (k + 1) % sides
            vs = [a[k] if len(a) > 1 else a[0], a[k2] if len(a) > 1 else None, b[k2] if len(b) > 1 else None, b[k] if len(b) > 1 else b[0]]
            vs = [v for j, v in enumerate(vs) if v is not None]
            vs = list(dict.fromkeys(vs))
            if len(vs) < 3:
                continue
            f = bm.faces.new(vs)
            keys[f] = keyfn(i, k) if keyfn else mat
            rm = (prof[i][0] + prof[(i + 1) % len(prof)][0]) / 2
            for l in f.loops:
                kk = k + (1 if l.vert in (a[k2] if len(a) > 1 else (), b[k2] if len(b) > 1 else ()) else 0)
                l[uvl].uv = (kk / sides * 2 * math.pi * max(rm, 0.02) / TILE * 2 + seed, l.vert.co.z / TILE * 2 + seed * 0.37)
    bm.transform(xf)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    merge_faces(pc, bm, uvl, lambda f: keys[f])

def tube(pc, mat, pts, r0, r1, sides=6, wob=0.0, seed=0.0):
    """A tapered round bar swept along pts (roots): radius r0 at the first point down to r1 at the last."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = []; n = len(pts)
    for i, p in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        s = d.cross(Vector((0, 0, 1)) if abs(d.z) < 0.9 else Vector((1, 0, 0))).normalized(); u = s.cross(d)
        t = i / max(1, n - 1); r = (r0 + (r1 - r0) * t ** 0.8)
        r *= 1 + wob * noise.noise(Vector((i * 0.7 + seed, seed, 1.3)))
        rings.append([bm.verts.new(p + (s * math.cos(2 * math.pi * k / sides) + u * math.sin(2 * math.pi * k / sides)) * r) for k in range(sides)])
    acc = 0.0
    for i in range(n - 1):
        seg = (pts[i + 1] - pts[i]).length
        for k in range(sides):
            f = bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
            for l in f.loops:
                l[uvl].uv = (k / sides * 0.6 * 2 * math.pi * r0 / TILE * 4, (acc + (seg if l.vert in rings[i + 1] else 0)) / TILE * 2)
        acc += seg
    bm.faces.new(rings[0][::-1]); bm.faces.new(rings[-1])
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    merge_faces(pc, bm, uvl, lambda f: mat)

def arc_stone(pc, mat, cx, cz, a_deg, r_in, r_out, tang, y_c, y_len, cham=0.018, R=None, proud=0.0, tilt=0.0, **kw):
    """One voussoir/vault stone at angle a (deg from +x about the arc centre (cx, cz)); radial x tangential box, inner face at r_in - proud."""
    a = math.radians(a_deg); rad = r_out - r_in; rc = r_in - proud + rad / 2
    pos = Vector((cx + rc * math.cos(a), y_c, cz + rc * math.sin(a)))
    rot = Ry(-a_deg) @ Rx(tilt)
    kw.setdefault('wob', 0.005); kw.setdefault('seed', (R or R_('arc')).random() * 90)
    box(pc, mat, (rad, y_len, tang), pos, rot, cham=cham, **kw)

def arc_ring(pc, mat, cx, cz, r_in, r_out, n, y_c, y_len, a0=0, a1=180, key=None, keystone=0.0, cham=0.02, tag='ring'):
    R = R_(tag)
    da = (a1 - a0) / n
    for i in range(n):
        am = a0 + da * (i + 0.5)
        tang = 2 * r_in * math.tan(math.radians(da / 2)) * 1.04
        kst = keystone if (n % 2 == 1 and i == n // 2) else 0.0
        yl = y_len + R.uniform(-0.03, 0.03)
        arc_stone(pc, mat, cx, cz, am, r_in - kst, r_out, tang, y_c + R.uniform(-0.01, 0.015), yl, cham=cham, R=R,
                  proud=0.0, tilt=R.uniform(-0.8, 0.8))

def stones_along(pc, mat, R, y0, y1, nmin, nmax, minlen, fn, gap=0.012):
    """Split [y0, y1] into stones (random seams, at least minlen) and call fn(ya, yb, R); outer ends inset by gap."""
    n = R.randint(nmin, nmax)
    cuts = sorted(R.uniform(y0 + minlen, y1 - minlen) for _ in range(n - 1))
    ok = [y0] + cuts + [y1]
    if any(ok[i + 1] - ok[i] < minlen for i in range(len(ok) - 1)):
        ok = [y0 + (y1 - y0) * i / n for i in range(n + 1)]
    for i in range(n):
        fn(ok[i] + gap / 2, ok[i + 1] - gap / 2, R)

def prism_bm(profile, y0, y1):
    """Closed prism of an (x, z) profile extruded along y from y0 to y1 (outward normals)."""
    bm = bmesh.new()
    f0 = [bm.verts.new((x, y0, z)) for x, z in profile]; f1 = [bm.verts.new((x, y1, z)) for x, z in profile]
    bm.faces.new(f0); bm.faces.new(f1[::-1])
    n = len(profile)
    for i in range(n):
        bm.faces.new((f0[i], f0[(i + 1) % n], f1[(i + 1) % n], f1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm

def arch_profile(cx, z0, w, z_spring, segs=16, flat_top=None):
    """Rectangle from z0 to z_spring with a semicircular head of radius w/2 (an arched opening)."""
    r = w / 2
    pts = [(cx - r, z0), (cx + r, z0), (cx + r, z_spring)]
    for i in range(1, segs):
        a = math.pi * i / segs
        pts.append((cx + r * math.cos(a), z_spring + r * math.sin(a)))
    pts.append((cx - r, z_spring))
    return pts

def wall_profile(r=R_OUT, segs=24):
    pts = [(-r, 0), (r, 0), (r, SPRING)]
    for i in range(1, segs):
        a = math.pi * i / segs
        pts.append((r * math.cos(a), SPRING + r * math.sin(a)))
    pts.append((-r, SPRING))
    return pts

def wall_body(pc, cutters, y0=-0.5, y1=0.0, back_key=None, uvfix=None):
    """Brick wall body of the vault section with arched cutters (boolean), chamfered rim; returns after merging into pc.
    Recess faces facing +y inside a cutter get limewash, the rest brick."""
    bm = prism_bm(wall_profile(), y0, y1)
    me = bpy.data.meshes.new('wb'); bm.to_mesh(me); bm.free()
    ob = bpy.data.objects.new('wb', me); bpy.context.scene.collection.objects.link(ob)
    cm = bmesh.new()
    for prof, cy0, cy1 in cutters:
        c = prism_bm(prof, cy0, cy1)
        vm = {v: cm.verts.new(v.co) for v in c.verts}
        for f in c.faces:
            cm.faces.new([vm[v] for v in f.verts])
        c.free()
    cme = bpy.data.meshes.new('cut'); cm.to_mesh(cme); cm.free()
    cob = bpy.data.objects.new('cut', cme); bpy.context.scene.collection.objects.link(cob)
    mod = ob.modifiers.new('b', 'BOOLEAN'); mod.operation = 'DIFFERENCE'; mod.object = cob; mod.solver = 'EXACT'
    bpy.context.view_layer.update()
    dg = bpy.context.evaluated_depsgraph_get()
    out = bmesh.new(); out.from_object(ob, dg)
    bpy.data.objects.remove(ob); bpy.data.objects.remove(cob); bpy.data.meshes.remove(me); bpy.data.meshes.remove(cme)
    front = [e for e in out.edges if abs(e.verts[0].co.y - y1) < 1e-4 and abs(e.verts[1].co.y - y1) < 1e-4]
    bmesh.ops.bevel(out, geom=front, offset=0.03, segments=1, affect='EDGES', profile=0.5)
    out.normal_update()
    uvl = out.loops.layers.uv.new('UVMap')
    FK._uv_project(out.faces, uvl, Matrix.Identity(4), None, (0, 0))
    if uvfix:
        for f in out.faces:
            uvfix(f, uvl)
    def key(f):
        c = f.calc_center_median()
        if f.normal.y > 0.9:
            if y1 - 0.07 < c.y < y1 - 0.02:
                return 'brick'            # the floor of a window in the render
            return 'lime'                  # the front face and the niche backs
        return 'brick'
    merge_faces(pc, out, uvl, key)

def blob(rect, R):
    """An irregular 9-gon (x, z) inside rect: the outline of a window where the limewash has come away."""
    x0, x1, z0, z1 = rect; cx, cz = (x0 + x1) / 2, (z0 + z1) / 2
    pts = []
    for i in range(9):
        a = 2 * math.pi * i / 9 + R.uniform(-0.15, 0.15); k = R.uniform(0.72, 1.0)
        pts.append((cx + (x1 - x0) / 2 * math.cos(a) * k, cz + (z1 - z0) / 2 * math.sin(a) * k))
    return pts

def windows(R, spec, avoid=(), rmax=2.36, xs=None, wr=(0.4, 1.05)):
    """Rects (x0, x1, z0, z1) of fallen render: spec = [(zlo, zhi, count)], none overlapping each other or the avoid rects."""
    out = []
    for zlo, zhi, n in spec:
        for _ in range(n):
            for _t in range(40):
                w = R.uniform(*wr); h = R.uniform(0.3, 0.65)
                cx = R.uniform(*(R.choice(xs) if xs else (-2.0, 2.0))); cz = R.uniform(zlo, zhi)
                if abs(cx) + w / 2 > rmax - 0.05 or (cz + h / 2 > SPRING and math.hypot(abs(cx) + w / 2, cz + h / 2 - SPRING) > rmax):
                    continue
                r = (cx - w / 2, cx + w / 2, cz - h / 2, cz + h / 2)
                if any(not (r[1] + 0.12 <= q[0] or r[0] - 0.12 >= q[1] or r[3] + 0.12 <= q[2] or r[2] - 0.12 >= q[3]) for q in out + list(avoid)):
                    continue
                out.append(r); break
    return out

def inner_hw(zlo, zhi, r=2.46):
    zt = zhi if zhi > SPRING else zlo
    if zhi <= SPRING:
        return r
    return math.sqrt(max(0.0, r * r - (zhi - SPRING) ** 2))

# ----------------------------------------------------------------------------- pieces
def p_vault_segment():
    pc = FK.Piece('VaultSegment'); R = R_('vseg')
    for side in (-1, 1):
        box(pc, 'mortar', (0.47, 2.0, 1.6), (side * 2.765, 1.0, 0.8), cham=0.0)
        for i in range(12):
            a = 90 / 12 * (i + 0.5)
            arc_stone(pc, 'mortar', 0, SPRING, (a if side > 0 else 180 - a), 2.56, 3.0, 2 * 2.56 * math.tan(math.radians(90 / 24)) * 1.1, 1.0, 2.0, cham=0.0, wob=0.0)
        # wall courses (old brick), a stone springing course corbelled in
        for k in range(7):
            z0 = 0.2 * k
            def stone(ya, yb, R, z0=z0, side=side):
                pr = R.uniform(0.0, 0.014)
                box(pc, 'brick', (0.54, yb - ya, 0.19), (side * (2.77 - pr), (ya + yb) / 2, z0 + 0.1), Rx(R.uniform(-0.5, 0.5)),
                    cham=0.018, wob=0.005, seed=R.random() * 90)
            stones_along(pc, 'brick', R, 0.0, MOD, 2, 3, 0.45, stone)
        def spring(ya, yb, R, side=side):
            box(pc, 'vault', (0.62, yb - ya, 0.2), (side * 2.72, (ya + yb) / 2, 1.5), Rx(R.uniform(-0.4, 0.4)), cham=0.03, wob=0.006, seed=R.random() * 90)
        stones_along(pc, 'vault', R, 0.0, MOD, 2, 2, 0.7, spring)
        # vault rows: stone rows with brick bands (every fourth), angles per side
        groups = [[10, 9, 8, 5], [9, 8, 7, 5], [9, 8, 7, 5]] if side > 0 else [[9, 8, 7, 5], [10, 9, 8, 5], [9, 7, 8, 5]]
        a0 = 0.0
        for gi, grp in enumerate(groups):
            for ri, w in enumerate(grp):
                brick = ri == 3
                am = a0 + w / 2
                tang = 2 * R_IN * math.tan(math.radians(w / 2)) * 1.05
                ang = am if side > 0 else 180 - am
                def vst(ya, yb, R, ang=ang, tang=tang, brick=brick):
                    arc_stone(pc, 'brick' if brick else 'vault', 0, SPRING, ang, R_IN, R_IN + 0.54, tang, (ya + yb) / 2, yb - ya,
                              cham=0.016 if brick else 0.022, R=R, proud=R.uniform(0.0, 0.016), tilt=R.uniform(-0.7, 0.7), wob=0.006)
                if brick:
                    stones_along(pc, 'brick', R, 0.0, MOD, 4, 5, 0.3, vst)
                else:
                    stones_along(pc, 'vault', R, 0.0, MOD, 2, 3, 0.5, vst)
                a0 += w
    return pc

def p_arch_rib():
    pc = FK.Piece('ArchRib'); R = R_('rib')
    arc_ring(pc, 'brick', 0, SPRING, 2.38, 3.0, 15, 0.0, 0.45, keystone=0.06, cham=0.022, tag='ribring')
    for side in (-1, 1):
        px = side * 2.69
        box(pc, 'vault', (0.74, 0.74, 0.22), (px, 0, 0.11), cham=0.04, wob=0.004, seed=1 + side)           # plinth
        for k, (z0, z1) in enumerate(((0.22, 0.60), (0.60, 1.00), (1.00, 1.42))):
            R2 = R_('pier%d%d' % (side, k))
            box(pc, 'vault', (0.62 + R2.uniform(-0.01, 0.03), 0.60 + R2.uniform(-0.01, 0.03), z1 - z0 - 0.012), (side * (2.69 + R2.uniform(-0.01, 0.01)), R2.uniform(-0.01, 0.01), (z0 + z1) / 2),
                Rz(R2.uniform(-1.0, 1.0)), cham=0.025, wob=0.005, seed=R2.random() * 80)
        box(pc, 'vault', (0.76, 0.76, 0.20), (px, 0, 1.51), cham=0.045, wob=0.004, seed=5 + side)            # cap
    return pc

def p_pillar():
    pc = FK.Piece('Pillar'); R = R_('pillar')
    box(pc, 'vault', (0.74, 0.74, 0.22), (0, 0, 0.11), cham=0.04, wob=0.004, seed=2)
    for k, (z0, z1) in enumerate(((0.22, 0.60), (0.60, 1.00), (1.00, 1.42))):
        R2 = R_('pil%d' % k)
        box(pc, 'vault', (0.6 + R2.uniform(-0.01, 0.025), 0.6 + R2.uniform(-0.01, 0.025), z1 - z0 - 0.012), (R2.uniform(-0.01, 0.01), R2.uniform(-0.01, 0.01), (z0 + z1) / 2),
            Rz(R2.uniform(-1.2, 1.2)), cham=0.025, wob=0.005, seed=R2.random() * 80)
    box(pc, 'vault', (0.76, 0.76, 0.20), (0, 0, 1.51), cham=0.045, wob=0.004, seed=6)
    return pc

def p_end_wall():
    pc = FK.Piece('EndWall'); R = R_('endwall')
    wins = windows(R, [(0.1, 0.9, 4), (0.9, 2.2, 3), (2.2, 3.4, 2)])
    wall_body(pc, [(blob(w, R), -0.045, 0.2) for w in wins], -0.5, 0.0)
    return pc

NICHE_W, NICHE_H, NICHE_D = 0.92, 0.80, 0.42
NICHE_X = (-1.3, 0.0, 1.3)
NICHE_Z = (0.35, 1.30, 2.25)
# bottles per niche (bottom row, second row, fallen, broken) indexed [row][col]
NICHE_FILL = [[(4, 2, 0, 0), (6, 4, 0, 0), (3, 1, 0, 0)],
              [(5, 2, 0, 0), (0, 0, 1, 0), (3, 0, 0, 1)],
              [(2, 0, 0, 0), (4, 2, 0, 0), (0, 0, 1, 1)]]

BOTTLE = [(0, 0), (0.040, 0.0), (0.052, 0.012), (0.052, 0.19), (0.036, 0.232), (0.019, 0.262), (0.017, 0.300), (0.023, 0.306), (0.023, 0.322), (0, 0.322)]

def bottle(pc, pos, yaw, roll, R, length_cut=None):
    prof = [(r * 1.12, t * 1.12) for r, t in BOTTLE] if length_cut is None else [(r * 1.12, t * 1.12) for r, t in BOTTLE if t <= length_cut] + [(0, length_cut * 1.12)]
    # local z = bottle axis; place the axis along +y (neck out): rotate -90 deg about x maps z -> +y
    xf = Matrix.Translation(Vector(pos)) @ Rz(yaw) @ Ry(roll) @ Rx(-90)     # roll spins it about its own axis
    lathe(pc, prof, xf, sides=8, mat='glass', seed=R.random() * 5)

def p_niche_wall():
    pc = FK.Piece('NicheWall'); R = R_('niche')
    cutters = []
    for z0 in NICHE_Z:
        for cx in NICHE_X:
            cutters.append((arch_profile(cx, z0, NICHE_W, z0 + NICHE_H - NICHE_W / 2, 12), -NICHE_D, 0.12))
    zone = [(cx - NICHE_W / 2 - 0.3, cx + NICHE_W / 2 + 0.3, z0 - 0.15, z0 + NICHE_H + 0.3) for z0 in NICHE_Z for cx in NICHE_X]
    wins = windows(R, [(0.1, 0.5, 1), (3.1, 3.5, 2)], avoid=zone)
    wins += windows(R, [(0.3, 2.9, 5)], avoid=zone + wins, xs=[(-2.12, -1.98), (1.98, 2.12)], wr=(0.36, 0.5))
    cutters += [(blob(w, R), -0.045, 0.2) for w in wins]
    def niche_uv(f, uvl):
        # the arch soffit of a niche: arc-length u (metres / TILE) and depth v, so the plaster is one continuous 2 m/tile surface
        # instead of per-facet planar projections (which smear where the dominant axis flips)
        if abs(f.normal.y) > 0.6:
            return
        c = f.calc_center_median()
        for z0 in NICHE_Z:
            zs = z0 + NICHE_H - NICHE_W / 2
            for cx in NICHE_X:
                if abs(c.x - cx) < NICHE_W / 2 + 0.02 and zs - 0.02 <= c.z <= zs + NICHE_W / 2 + 0.02 and -NICHE_D - 0.01 <= c.y <= 0.0:
                    for l in f.loops:
                        a = math.atan2(l.vert.co.z - zs, l.vert.co.x - cx)
                        l[uvl].uv = (a * (NICHE_W / 2) / FK.TILE + 0.37 * z0, l.vert.co.y / FK.TILE)
                    return
    wall_body(pc, cutters, -0.5, 0.0, uvfix=niche_uv)
    # sills (vault stone) project in front of each niche; brick-red arch rings of voussoirs around the heads
    for ri, z0 in enumerate(NICHE_Z):
        for ci, cx in enumerate(NICHE_X):
            box(pc, 'vault', (NICHE_W + 0.2, NICHE_D + 0.12, 0.07), (cx, -NICHE_D / 2 + 0.06, z0 - 0.025), Rx(R.uniform(-0.8, 0.8)), cham=0.025, wob=0.004, seed=R.random() * 90)
            zs = z0 + NICHE_H - NICHE_W / 2
            n = 5
            for i in range(n):
                am = 180 / n * (i + 0.5)
                tang = 2 * (NICHE_W / 2) * math.tan(math.radians(180 / n / 2)) * 1.02
                # ring stops at r 0.46 + 0.12: 0.14 m of plain wall (a pier) stays between neighbouring rings
                # seated: inner radius 0.02 inside the niche opening, 0.07 m embedded into the wall face (y -0.07..0.05), tangential 1.07 so joints close
                arc_stone(pc, 'brick', cx, zs, am, NICHE_W / 2 - 0.02, NICHE_W / 2 + 0.12, tang * 1.05, -0.01, 0.12, cham=0.012, R=R, tilt=R.uniform(-0.3, 0.3), wob=0.0015)
    # (the render windows are part of the wall body cutters above)
    # bottles lying in the niches (neck out, y -0.36 .. -0.04)
    for ri, z0 in enumerate(NICHE_Z):
        for ci, cx in enumerate(NICHE_X):
            nb, nt, nf, nbr = NICHE_FILL[ri][ci]
            Rb = R_('bot%d%d' % (ri, ci))
            def put(n, z, off):
                for i in range(n):
                    x = cx - 0.36 + 0.115 * i + off + Rb.uniform(-0.01, 0.01)
                    bottle(pc, (x, -0.375 + Rb.uniform(-0.01, 0.02), z), Rb.uniform(-7, 7), Rb.uniform(0, 360), Rb)
            put(nb, z0 + 0.055, 0.0)
            put(nt, z0 + 0.16, 0.0575)
            for i in range(nf):      # a bottle fallen over, lying across the niche
                bottle(pc, (cx - 0.1, -0.12, z0 + 0.052), 62, 0, Rb)
            for i in range(nbr):     # broken: just the base halves remain
                bottle(pc, (cx - 0.15, -0.36, z0 + 0.052), 4, 0, Rb, length_cut=0.13)
                bottle(pc, (cx + 0.12, -0.34, z0 + 0.052), -9, 0, Rb, length_cut=0.09)
    return pc

N_STEPS, STEP_H = 13, 0.2
STEP_D = 3.5 / N_STEPS

def p_stair_entry():
    pc = FK.Piece('StairEntry'); R = R_('stair')
    wins = windows(R, [(0.1, 0.9, 3), (0.9, 2.2, 2), (2.4, 3.4, 2)], avoid=[(-1.05, 1.05, 0.0, 2.6)])
    wall_body(pc, [(arch_profile(0, 0.0, 1.2, SPRING, 16), -0.7, 0.12)] + [(blob(w, R), -0.045, 0.2) for w in wins], -0.5, 0.0)
    # door arch ring (brick voussoirs) proud of the face
    for i in range(7):
        am = 180 / 7 * (i + 0.5)
        tang = 2 * 0.6 * math.tan(math.radians(180 / 7 / 2)) * 1.06
        arc_stone(pc, 'brick', 0, SPRING, am, 0.6, 0.82, tang, 0.0, 0.14, cham=0.02, R=R, tilt=R.uniform(-1, 1), wob=0.004)
    for sx in (-1, 1):   # jamb stones
        for k in range(4):
            box(pc, 'brick', (0.22 + R.uniform(0, 0.04), 0.14, 0.38), (sx * (0.71 + R.uniform(0, 0.02)), 0.0, 0.19 + 0.4 * k), Rx(R.uniform(-0.6, 0.6)), cham=0.02, wob=0.004, seed=R.random() * 90)
    # stone steps rising toward -y out of the room
    for k in range(N_STEPS):
        zt = STEP_H * (k + 1); y1 = -0.5 - k * STEP_D
        box(pc, 'vault', (1.2 + R.uniform(0, 0.03), STEP_D + 0.01, zt), (R.uniform(-0.012, 0.012), y1 - STEP_D / 2, zt / 2), Rz(R.uniform(-0.7, 0.7)), cham=0.032, segs=2, wob=0.004, seed=R.random() * 90)
    slope = (N_STEPS * STEP_H - STEP_H) / (3.5 - STEP_D)
    nose = lambda y: STEP_H + slope * (-0.5 - y)
    top = lambda y: nose(y) + 2.1
    ya, yb = -0.5, -4.0
    for sx in (-1, 1):   # side walls (brick) following the stair
        prof = [(ya, 0.0), (yb, 0.0), (yb, top(yb)), (ya, top(ya))]
        bm = bmesh.new()
        f0 = [bm.verts.new((sx * 0.6, y, z)) for y, z in prof]; f1 = [bm.verts.new((sx * 1.0, y, z)) for y, z in prof]
        bm.faces.new(f0); bm.faces.new(f1[::-1])
        for i in range(4):
            bm.faces.new((f0[i], f0[(i + 1) % 4], f1[(i + 1) % 4], f1[i]))
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
        bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.025, segments=1, affect='EDGES')
        uvl = bm.loops.layers.uv.new('UVMap'); bm.normal_update()
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
        merge_faces(pc, bm, uvl, lambda f: 'brick')
    prof = [(ya, top(ya) - 0.05), (yb, top(yb) - 0.05), (yb, top(yb) + 0.35), (ya, top(ya) + 0.35)]
    bm = bmesh.new()
    f0 = [bm.verts.new((-1.0, y, z)) for y, z in prof]; f1 = [bm.verts.new((1.0, y, z)) for y, z in prof]
    bm.faces.new(f0[::-1]); bm.faces.new(f1)
    for i in range(4):
        bm.faces.new((f0[(i + 1) % 4], f0[i], f1[i], f1[(i + 1) % 4]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    bmesh.ops.bevel(bm, geom=bm.edges[:], offset=0.03, segments=1, affect='EDGES')
    uvl = bm.loops.layers.uv.new('UVMap'); bm.normal_update()
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    merge_faces(pc, bm, uvl, lambda f: 'vault')
    return pc

def p_floor_tile():
    pc = FK.Piece('FloorTile'); R = R_('floor')
    box(pc, 'mortar', (2.0, 2.0, 0.1), (0, 1.0, -0.09), cham=0.0)
    ycuts = [0.0, R.uniform(0.55, 0.75), R.uniform(1.2, 1.45), 2.0]
    for ri in range(3):
        y0, y1 = ycuts[ri], ycuts[ri + 1]
        n = R.choice((2, 2, 3))
        xc = [-1.0] + sorted(R.uniform(-0.45, 0.55) for _ in range(n - 1)) + [1.0]
        if any(xc[i + 1] - xc[i] < 0.45 for i in range(n)):
            xc = [-1.0 + 2.0 * i / n for i in range(n + 1)]
        for ci in range(n):
            x0, x1 = xc[ci] + 0.016, xc[ci + 1] - 0.016
            yy0, yy1 = y0 + 0.016, y1 - 0.016
            sx, sy = x1 - x0, yy1 - yy0
            top = -0.004 + R.uniform(-0.006, 0.006)
            def dish(p, sx=sx, sy=sy):
                if p.z < 0.0:
                    return Vector()
                w = max(0.0, 1 - (2 * p.x / sx) ** 2 - (2 * p.y / sy) ** 2)
                return Vector((0, 0, -0.016 * w))
            box(pc, 'flag', (sx, sy, 0.12), ((x0 + x1) / 2, (yy0 + yy1) / 2, top - 0.06), Rz(R.uniform(-0.8, 0.8)), cham=0.028, segs=2, cuts=(2, 2, 0),
                bend=dish, wob=0.004, seed=R.random() * 90)
    return pc

def p_breath_grate():
    pc = FK.Piece('BreathGrate'); R = R_('grate')
    FK.quad_grid(pc, 'breath', [(-0.5, -0.5, 0.006), (0.5, -0.5, 0.006), (0.5, 0.5, 0.006), (-0.5, 0.5, 0.006)], 4, 4, uv_fn=lambda c: (c.x, c.y))
    for sx in (-1, 1):
        box(pc, 'iron', (0.13, 1.2, 0.05), (sx * 0.535, 0, 0.025), Rz(sx * R.uniform(0.3, 0.8)), cham=0.012, wob=0.003, seed=sx + 3)
    for sy in (-1, 1):
        box(pc, 'iron', (0.94, 0.13, 0.05), (0, sy * 0.535, 0.025), cham=0.012, wob=0.003, seed=sy + 9)
    for i in range(7):
        x = -0.42 + i * 0.14
        box(pc, 'iron', (0.07, 1.02, 0.045), (x + R.uniform(-0.008, 0.008), 0, 0.022), Rz(R.uniform(-0.7, 0.7)), cham=0.011, wob=0.002, seed=i + 20)
    for y in (-0.2, 0.2):
        box(pc, 'iron', (0.94, 0.06, 0.035), (0, y, 0.016), cham=0.009, wob=0.002, seed=y + 30)
    # a cracked flag collar around the grate
    for sx, sy, w, d in ((-1, 0, 0.1, 1.0), (1, 0, 0.1, 1.0), (0, -1, 1.0, 0.1), (0, 1, 1.0, 0.1)):
        pass
    return pc

def p_roots():
    pc = FK.Piece('Roots'); R = R_('roots')
    rng_pts = lambda x0, y0, L, dx, dy, n, wig, seed: [Vector((x0 + dx * (i / n) + wig * noise.noise(Vector((i * 0.5 + seed, seed, 2.0))),
                                                              y0 + dy * (i / n) + wig * noise.noise(Vector((seed, i * 0.5 + seed, 5.0))),
                                                              -L * (i / n) + 4.1 * 0 - 0.0)) for i in range(n + 1)]
    # the crack: a jagged dark ribbon along the vault crown (the roots come through it)
    pts = []
    for i in range(14):
        y = -0.5 + i * 0.077
        pts.append((0.025 * math.sin(i * 1.7) + R.uniform(-0.02, 0.02), y))
    for (xa, ya), (xb, yb) in zip(pts[:-1], pts[1:]):
        w = 0.05 + R.uniform(0, 0.04)
        zs = lambda x: -x * x / 5.0 - 0.002
        FK.quad_grid(pc, 'soot', [(xa - w, ya, zs(xa - w)), (xb - w, yb, zs(xb - w)), (xb + w, yb, zs(xb + w)), (xa + w, ya, zs(xa + w))], 1, 1, uv_fn=lambda c: (c.x, c.y))   # faces down
    mains = [(0.00, -0.30, 1.55, 0.10, 0.12), (0.04, -0.10, 1.25, -0.12, 0.05), (-0.03, 0.08, 1.70, 0.15, -0.10), (0.02, 0.28, 1.1, -0.08, 0.14), (-0.05, 0.40, 0.9, 0.10, -0.05), (0.05, -0.45, 0.8, -0.06, -0.08), (-0.02, 0.0, 1.0, 0.2, 0.2)]
    for i, (x0, y0, L, dx, dy) in enumerate(mains):
        n = 11
        p = rng_pts(x0, y0, L, dx, dy, n, 0.07, i * 3.1)
        r0 = 0.075 + 0.02 * R.random()
        tube(pc, 'root', p, r0, 0.01, sides=6, wob=0.25, seed=i * 2.3)
        for j in range(3):   # side roots
            t = R.randint(3, 7); q0 = p[t]
            dirv = Vector((R.uniform(-0.25, 0.25), R.uniform(-0.25, 0.25), -R.uniform(0.35, 0.6)))
            bp = [q0 + dirv * (m / 6.0) + Vector((0.05 * noise.noise(Vector((m * 0.7 + i, j, 3.0))), 0.05 * noise.noise(Vector((m * 0.7, j + i, 8.0))), 0)) for m in range(7)]
            tube(pc, 'root', bp, r0 * 0.5, 0.007, sides=5, wob=0.2, seed=i + j)
    # creeping roots along the vault surface
    for i, (sgn, y0, ln) in enumerate(((1, -0.2, 0.9), (-1, 0.05, 1.0), (1, 0.3, 0.7), (-1, -0.4, 0.8))):
        n = 9; sp = []
        for m in range(n + 1):
            phi = sgn * (ln * m / n) / R_IN
            sp.append(Vector(((R_IN - 0.015) * math.sin(phi), y0 + 0.14 * math.sin(m * 0.9 + i) + 0.05 * m * (i - 1.5) * 0.2, SPRING + (R_IN - 0.015) * math.cos(phi) - 4.1)))
        tube(pc, 'root', sp, 0.05, 0.012, sides=5, wob=0.25, seed=i + 40)
    return pc

def p_larder_shelf():
    pc = FK.Piece('LarderShelf'); R = R_('shelf')
    X0, X1 = -2.5, -2.08
    boards = (0.26, 0.82, 1.38, 1.94)
    for zi, z in enumerate(boards):
        def sag(p):
            return Vector((0, 0, -0.012 * (1 - (p.y / 0.97) ** 2)))
        box(pc, 'oak', (X1 - X0, 1.96, 0.06), ((X0 + X1) / 2, 1.0, z), Ry(R.uniform(-0.6, 0.6)), cham=0.012, cuts=(0, 3, 0), bend=sag, wob=0.003, seed=zi, long_axis=1, uv_world=False)
        for by in (0.35, 1.65):          # iron brackets: a diagonal strap and a bar under the board
            box(pc, 'iron', (0.32, 0.04, 0.03), (X0 + 0.16, by, z - 0.045), cham=0.006, wob=0.001, seed=by)
            box(pc, 'iron', (0.03, 0.04, 0.30), (X0 + 0.04, by, z - 0.19), cham=0.006, wob=0.001, seed=by + 1)
            FK.member(pc, 'iron', (X0 + 0.04, by, z - 0.34), (X0 + 0.30, by, z - 0.04), 0.035, 0.03, name='br%d%f' % (zi, by), cham=0.005, wob=0.001)
    for py in (0.07, 1.93):
        FK.member(pc, 'oak', (X1 - 0.05, py, 0.0), (X1 - 0.05, py, 2.2), 0.10, 0.10, name='post%f' % py, cham=0.018, wob=0.003, up=(1, 0, 0))
    # goods per shelf: (kind, x, y, scale, seed)
    def jar(x, y, z, s, glazed, seed, kind='jar'):
        Rj = R_('jar%f' % seed)
        if kind == 'jar':
            prof = [(0, 0), (0.095, 0.0), (0.12, 0.05), (0.13, 0.14), (0.11, 0.24), (0.075, 0.285), (0.082, 0.30), (0.082, 0.32), (0, 0.32)]
            sl = 5
        elif kind == 'crock':
            prof = [(0, 0), (0.15, 0.0), (0.185, 0.08), (0.2, 0.22), (0.19, 0.32), (0.205, 0.34), (0.205, 0.36), (0.16, 0.36), (0.16, 0.34), (0, 0.33)]
            sl = 5
        else:   # tall bottle-shaped pot
            prof = [(0, 0), (0.07, 0.0), (0.085, 0.04), (0.085, 0.2), (0.06, 0.27), (0.04, 0.31), (0.04, 0.37), (0.05, 0.38), (0.05, 0.40), (0, 0.40)]
            sl = 4
        gl = glazed
        def key(i, k, sl=sl, gl=gl, Rj=Rj):
            if i > sl: return gl
            if i == sl: return gl if ((k * 7 + int(seed * 10)) % 5) < 3 else 'clay'
            return 'clay'
        lathe(pc, [(r * s, h * s) for r, h in prof], T(x, y, z + 0.03) @ Rz(Rj.uniform(0, 90)), sides=10, keyfn=key, seed=seed)
    z0 = boards[0] + 0.03
    jar(-2.30, 0.38, z0, 1.2, 'glaze', 1.1, 'crock'); jar(-2.30, 0.88, z0, 1.2, 'glaze2', 2.2, 'crock')
    lathe(pc, [(0, 0), (0.2, 0.0), (0.21, 0.02), (0.21, 0.12), (0.19, 0.14), (0, 0.14)], T(-2.30, 1.52, z0 + 0.03), sides=12, mat='cheese', seed=3.0)
    jar(-2.28, 1.88, z0, 1.0, 'glaze', 4.4, 'jar')
    z1 = boards[1] + 0.03
    for i, (y, g) in enumerate(((0.22, 'glaze2'), (0.52, 'glaze'), (0.82, 'glaze'), (1.14, 'glaze2'), (1.46, 'glaze'), (1.78, 'glaze2'))):
        jar(-2.31 + 0.02 * (i % 2), y, z1, 0.95 + 0.1 * (i % 3) / 2, g, 5 + i, 'jar' if i % 2 == 0 else 'tall')
    z2 = boards[2] + 0.03
    for i, y in enumerate((0.25, 0.55, 0.85)):
        jar(-2.32, y, z2, 1.05, 'glaze' if i != 1 else 'glaze2', 12 + i, 'tall')
    jar(-2.30, 1.30, z2, 1.1, 'glaze2', 15, 'crock'); jar(-2.30, 1.78, z2, 0.9, 'glaze', 16, 'jar')
    z3 = boards[3] + 0.03
    for i, y in enumerate((0.25, 0.5, 0.75, 1.05, 1.35, 1.65, 1.85)):
        jar(-2.33 + 0.02 * (i % 2), y, z3, 0.75 + 0.08 * (i % 3), 'glaze' if i % 3 else 'glaze2', 20 + i, 'jar')
    return pc

def p_coal_chute():
    pc = FK.Piece('CoalChute'); R = R_('chute')
    # hatch on the vault surface at 21 deg above the springing: local x radial out, z up the surface
    a = 21.0; ar = math.radians(a)
    surf = Vector((2.5 * math.cos(ar), 0.0, SPRING + 2.5 * math.sin(ar)))
    xfh = Matrix.Translation(surf) @ Ry(-a)
    def hb(size, c, tilt=0.0, mat='iron', cham=0.014):
        FK.add_box(pc, mat, size, xfh @ Matrix.Translation(Vector(c)) @ Rx(tilt), cham=cham, uv_world=True, wob=0.003, seed=R.random() * 90)
    HY, HZ = 0.46, 0.38            # half opening (along the vault, up the surface)
    hb((0.14, 2 * HY + 0.22, 0.12), (-0.05, 0.0, HZ + 0.06)); hb((0.14, 2 * HY + 0.22, 0.12), (-0.05, 0.0, -HZ - 0.06), 0.5)
    hb((0.14, 0.12, 2 * HZ), (-0.05, HY + 0.06, 0.0)); hb((0.14, 0.12, 2 * HZ), (-0.05, -HY - 0.06, 0.0))
    for sy in (-1, 1):
        for sz in (-1, 1):
            hb((0.06, 0.07, 0.07), (-0.13, sy * (HY + 0.06), sz * (HZ + 0.06)), cham=0.008)
    hb((0.05, 0.05, 2 * HZ), (-0.11, 0.0, 0.0), cham=0.008)               # centre bar across the hatch
    cw = [xfh @ Vector(v) for v in ((-0.012, -HY, -HZ), (-0.012, -HY, HZ), (-0.012, HY, -HZ))]
    c4 = xfh @ Vector((-0.012, HY, HZ))
    FK.quad_grid(pc, 'daylight', [cw[0], cw[1], c4, cw[2]], 1, 1, uv_fn=lambda c: (c.y, c.z))
    # chute: from the hatch lip to a kicked foot, sides and iron straps, a hood over the hatch
    top = Vector((2.20, 0.0, 2.10)); foot = Vector((1.50, 0.0, 0.48))
    d = (foot - top).normalized(); nrm = Vector((-d.z, 0, d.x)); nrm = -nrm if nrm.x > 0 else nrm
    W = 0.80
    FK.member(pc, 'oak', top, foot, W, 0.08, name='chute_bed', cham=0.014, wob=0.004, up=nrm)
    for sy in (-1, 1):
        FK.member(pc, 'oak', top + Vector((0, sy * (W / 2 + 0.035), 0)) + nrm * 0.10, foot + Vector((0, sy * (W / 2 + 0.035), 0)) + nrm * 0.10, 0.07, 0.22,
                  name='chute_side%d' % sy, cham=0.016, wob=0.004, up=nrm)
    for t in (0.15, 0.45, 0.75):
        p0 = top + (foot - top) * t + nrm * 0.045
        FK.member(pc, 'iron', p0 + Vector((0, -(W / 2 + 0.11), 0)), p0 + Vector((0, W / 2 + 0.11, 0)), 0.09, 0.03, name='strap%f' % t, cham=0.008, wob=0.001, up=nrm)
    FK.member(pc, 'oak', foot + Vector((0.0, 0, 0.0)), foot + Vector((-0.34, 0, -0.10)), W, 0.07, name='lip', cham=0.014, wob=0.004, up=Vector((0.3, 0, 1)).normalized())
    FK.member(pc, 'oak', Vector((2.46, 0, 3.0)), Vector((2.05, 0, 2.62)), 1.02, 0.06, name='hood', cham=0.014, wob=0.004, up=Vector((-0.7, 0, 1)).normalized())
    for sy in (-1, 1):
        FK.member(pc, 'oak', Vector((2.46, sy * 0.5, 3.0)), Vector((2.06, sy * 0.5, 2.62)), 0.06, 0.4, name='hood_side%d' % sy, cham=0.012, wob=0.004, up=Vector((0, sy, 0)))
    for sy in (-1, 1):   # legs and wall braces
        FK.member(pc, 'oak', Vector((1.28, sy * 0.46, 0.0)), Vector((1.40, sy * 0.46, 0.80)), 0.13, 0.13, name='leg%d' % sy, cham=0.022, wob=0.004, up=(1, 0, 0))
        FK.member(pc, 'oak', Vector((2.46, sy * 0.46, 1.35)), Vector((1.80, sy * 0.46, 0.84)), 0.11, 0.11, name='brace%d' % sy, cham=0.02, wob=0.004, up=(0, 1, 0))
    # coal heap at the foot, spilling out
    cx0 = 1.18
    for i in range(70):
        ang = R.uniform(0, 2 * math.pi); rho = 0.68 * math.sqrt(R.random())
        h = 0.55 * max(0.0, 1 - rho / 0.82) ** 0.75
        s = R.uniform(0.11, 0.24) * (1.0 if rho < 0.5 else 0.72)
        c = Vector((cx0 + 0.15 + rho * math.cos(ang), 0.68 * rho * math.sin(ang), h + s * 0.30))
        if c.x < 0.92:
            continue            # keep clear of the breath grate (Unity x 0..1.2 at z 5.6..6.8, 0.7 m from the chute)
        rot = Rx(R.uniform(0, 90)) @ Ry(R.uniform(0, 90)) @ Rz(R.uniform(0, 90))
        FK.add_box(pc, 'soot', (s, s * R.uniform(0.7, 1.1), s * R.uniform(0.6, 0.95)), Matrix.Translation(c) @ rot, cham=0.035, uv_world=True, wob=0.016, seed=R.random() * 90)
    for i in range(14):
        ang = R.uniform(0, 2 * math.pi); rho = R.uniform(0.75, 1.2); s = R.uniform(0.05, 0.10)
        if cx0 + 0.2 + rho * math.cos(ang) * 0.8 < 0.92:
            continue
        FK.add_box(pc, 'soot', (s, s, s * 0.7), Matrix.Translation(Vector((cx0 + 0.2 + rho * math.cos(ang) * 0.8, 0.6 * rho * math.sin(ang), s * 0.3))) @ Rz(R.uniform(0, 90)), cham=0.018,
                   uv_world=True, wob=0.01, seed=R.random() * 90)
    return pc

def barrel_prof(h, r_end=0.34, r_mid=0.41):
    rr = lambda t: r_end + (r_mid - r_end) * (1 - (2 * t - 1) ** 2)
    body = [(rr(t) * h / 1.0, t * h) for t in (0.0, 0.12, 0.3, 0.5, 0.7, 0.88, 1.0)]
    body = [(rr(t), t * h) for t in (0.0, 0.12, 0.3, 0.5, 0.7, 0.88, 1.0)]
    return ([(0, 0.055), (r_end - 0.04, 0.055), (r_end - 0.04, 0.0)] + body +
            [(r_end - 0.04, h), (r_end - 0.04, h - 0.055), (0, h - 0.055)]), rr

def barrel(pc, xf, h=1.0, seed=0):
    prof, rr = barrel_prof(h)
    lathe(pc, prof, xf, sides=16, mat='barrel', seed=seed)
    for t in (0.07, 0.27, 0.73, 0.93):
        r = rr(t) + 0.008
        lathe(pc, [(r, t * h - 0.028), (r + 0.026, t * h - 0.028), (r + 0.026, t * h + 0.028), (r, t * h + 0.028)], xf, sides=16, mat='iron', closed=True, seed=seed)

def p_barrels():
    pc = FK.Piece('Barrels'); R = R_('barrels')
    for sy in (-1, 1):   # cradle rails and chocks
        pass
    for sx in (-1, 1):
        box(pc, 'oak', (0.13, 1.98, 0.12), (sx * 0.30, 0.0, 0.06), Rz(R.uniform(-0.7, 0.7)), cham=0.02, wob=0.004, seed=sx, uv_world=False, long_axis=1)
        for by in (-0.45, 0.45):
            for cy in (-0.30, 0.30):
                box(pc, 'oak', (0.13, 0.17, 0.17), (sx * 0.30, by + cy, 0.17), Rx(45) @ Rz(R.uniform(-1.5, 1.5)), cham=0.015, wob=0.003, seed=sx + by + cy)
    for k, by in enumerate((-0.45, 0.45)):
        # lying: lathe axis along x, centre at z 0.50
        xf = Matrix.Translation(Vector((0.5, by + R.uniform(-0.01, 0.01), 0.50))) @ Rz(R.uniform(-1.5, 1.5)) @ Ry(-90)
        barrel(pc, xf, 1.0, seed=k * 3)
        box(pc, 'oak', (0.07, 0.07, 0.06), (0.0, by, 0.50 + 0.41), cham=0.01, wob=0.002, seed=k)       # bung
        box(pc, 'iron', (0.05, 0.05, 0.02), (0.0, by, 0.50 + 0.445), cham=0.006, wob=0.001, seed=k + 7)
    # a tap on one head
    FK.member(pc, 'iron', Vector((-0.50, 0.45, 0.30)), Vector((-0.60, 0.45, 0.30)), 0.04, 0.04, name='tap', cham=0.006, wob=0.001, up=(0, 0, 1))
    FK.member(pc, 'iron', Vector((-0.60, 0.45, 0.30)), Vector((-0.60, 0.45, 0.22)), 0.03, 0.03, name='tap2', cham=0.006, wob=0.001, up=(1, 0, 0))
    barrel(pc, Matrix.Translation(Vector((0.05, 1.50, 0.0))) @ Rz(R.uniform(0, 90)), 1.05, seed=9)
    return pc

# ----------------------------------------------------------------------------- stair kit (Q13 undercroft stair, exported as CellarStair.fbx)
# Same authoring frame (A = x across, y along, z up = Unity x, z, y). Contract: Reference/hollowmere/undercroft_stair/CONTRACT.md.
# Every flight descends toward +y (Unity +Z): step k top z = -0.2 (k + 1) over y 0.25 k .. 0.25 (k + 1); nosing line z = -0.8 y.
S_N, S_RISE, S_RUN = 8, 0.2, 0.25
SAIL_R = 0.85

def s_nose(y):
    return -0.8 * y

def prism_ax(profile, axis, a0, a1):
    """Closed prism of a 2D profile extruded along an axis from a0 to a1: 'x' (profile = (y, z)), 'y' ((x, z)) or 'z' ((x, y))."""
    bm = bmesh.new()
    P = {'x': lambda u, v, a: (a, u, v), 'y': lambda u, v, a: (u, a, v), 'z': lambda u, v, a: (u, v, a)}[axis]
    f0 = [bm.verts.new(P(u, v, a0)) for u, v in profile]; f1 = [bm.verts.new(P(u, v, a1)) for u, v in profile]
    bm.faces.new(f0); bm.faces.new(f1[::-1])
    n = len(profile)
    for i in range(n):
        bm.faces.new((f0[i], f0[(i + 1) % n], f1[(i + 1) % n], f1[i]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    return bm

def solid(pc, bm, key, cham=0.025):
    """Chamfer the sharp edges of a closed bmesh, project world metre UVs and merge it into pc (key = material key or fn(face))."""
    bm.normal_update()
    if cham > 0:
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0) > 0.5]
        bmesh.ops.bevel(bm, geom=edges, offset=cham, segments=1, affect='EDGES', profile=0.5)
    bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0))
    merge_faces(pc, bm, uvl, key if callable(key) else (lambda f: key))

def band_profile(cx, cz, r_in, r_out, n=16):
    """The upper half annulus r_in..r_out about (cx, cz) as an (x, z) polygon (legs end flat at z = cz)."""
    outer = [(cx + r_out * math.cos(math.pi * i / n), cz + r_out * math.sin(math.pi * i / n)) for i in range(n + 1)]
    inner = [(cx + r_in * math.cos(math.pi * i / n), cz + r_in * math.sin(math.pi * i / n)) for i in range(n, -1, -1)]
    return outer + inner

def arc_stone_x(pc, mat, cy, cz, a_deg, r_in, r_out, tang, x_c, x_len, cham=0.018, R=None, tilt=0.0, **kw):
    """Voussoir of an arch in the y-z plane (axis along x), angle a from +y about (cy, cz): the ring that arc_stone makes for arches along y."""
    a = math.radians(a_deg); rad = r_out - r_in; rc = r_in + rad / 2
    pos = Vector((x_c, cy + rc * math.cos(a), cz + rc * math.sin(a)))
    kw.setdefault('wob', 0.005); kw.setdefault('seed', (R or R_('arcx')).random() * 90)
    box(pc, mat, (x_len, rad, tang), pos, Rx(a_deg) @ Ry(tilt), cham=cham, **kw)

def arc_ring_x(pc, mat, cy, cz, r_in, r_out, n, x_c, x_len, cham=0.02, tag='ringx'):
    R = R_(tag); da = 180.0 / n
    for i in range(n):
        tang = 2 * r_in * math.tan(math.radians(da / 2)) * 1.04
        arc_stone_x(pc, mat, cy, cz, da * (i + 0.5), r_in, r_out, tang, x_c + R.uniform(-0.01, 0.01), x_len + R.uniform(-0.02, 0.02),
                    cham=cham, R=R, tilt=R.uniform(-0.8, 0.8))

def flags(pc, R, x0, x1, y0, y1, rows, top=0.0):
    """Irregular flagstones (top at z = top) over the rectangle, mortar bed below."""
    box(pc, 'mortar', (x1 - x0, y1 - y0, 0.2), ((x0 + x1) / 2, (y0 + y1) / 2, top - 0.14), cham=0.0)
    ys = [y0] + [y0 + (y1 - y0) * i / rows + R.uniform(-0.07, 0.07) for i in range(1, rows)] + [y1]
    for ri in range(rows):
        n = max(2, int(round((x1 - x0) / 0.62)))
        xc = [x0] + sorted(x0 + (x1 - x0) * i / n + R.uniform(-0.1, 0.1) for i in range(1, n)) + [x1]
        for ci in range(n):
            xa, xb = xc[ci] + 0.016, xc[ci + 1] - 0.016; ya, yb = ys[ri] + 0.016, ys[ri + 1] - 0.016
            sx, sy = xb - xa, yb - ya
            def dish(p, sx=sx, sy=sy):
                if p.z < 0.0:
                    return Vector()
                return Vector((0, 0, -0.016 * max(0.0, 1 - (2 * p.x / sx) ** 2 - (2 * p.y / sy) ** 2)))
            t = top - 0.004 + R.uniform(-0.006, 0.006)
            box(pc, 'flag', (sx, sy, 0.12), ((xa + xb) / 2, (ya + yb) / 2, t - 0.06), Rz(R.uniform(-0.8, 0.8)), cham=0.028, segs=1, cuts=(2, 2, 0),
                bend=dish, wob=0.004, seed=R.random() * 90)

def p_stair_flight():
    """8 steps, run 2.0, drop 1.6; origin = centre of the top edge at the previous tread level."""
    pc = FK.Piece('StairFlight'); R = R_('sflight')
    for k in range(S_N):                                   # treads: vault stone blocks, per-step jitter like StairEntry
        zt = -S_RISE * (k + 1)
        box(pc, 'vault', (1.2 + R.uniform(0, 0.03), S_RUN + 0.01, 0.5), (R.uniform(-0.012, 0.012), S_RUN * k + S_RUN / 2 + 0.005, zt - 0.25),
            Rz(R.uniform(-0.7, 0.7)), cham=0.032, segs=2, wob=0.004, seed=R.random() * 90)
    top = lambda y: s_nose(y) + 1.9                        # wall tops stay above every ring's springing (contract: nosing + 1.75)
    for sx in (-1, 1):                                     # sloped brick side walls
        bm = prism_ax([(0.0, -1.8), (2.0, -1.8), (2.0, top(2.0)), (0.0, top(0.0))], 'x', 0.6 if sx > 0 else -1.0, 1.0 if sx > 0 else -0.6)
        solid(pc, bm, 'lime', cham=0.025)                  # worn limewash body (brick stays for the arch bands)
        for j in range(5):                                 # wall stones, nearly flush (0.015 proud of the wall face at |x| 0.6)
            y = R.uniform(0.2, 1.8)
            box(pc, 'lime', (0.05, R.uniform(0.22, 0.34), R.uniform(0.1, 0.16)), (sx * 0.61, y, s_nose(y) + R.uniform(0.35, 1.3)), Rx(R.uniform(-0.6, 0.6)),
                cham=0.01, wob=0.002, seed=R.random() * 90)
    for i in range(4):                                     # vault: stepped rings (0.4 down per 0.5 m) on a continuous mortar backing
        sp = 1.5 - 0.4 * i
        solid(pc, prism_ax(band_profile(0.0, sp, 0.64, 1.0), 'y', 0.5 * i - 0.01, 0.5 * (i + 1) + 0.01), 'mortar', cham=0.0)
        arc_ring(pc, 'vault', 0, sp, 0.6, 1.0, 11, 0.5 * i + 0.25, 0.5, keystone=0.04, cham=0.02, tag='sfr%d' % i)
    arc_ring(pc, 'brick', 0, 1.5, 0.56, 0.82, 9, 0.1, 0.2, keystone=0.0, cham=0.02, tag='sfband')   # proud brick arch band at the top face
    return pc

def sail_z(x, y):
    xc = min(0.6, max(-0.6, x)); yc = min(1.2, max(0.0, y))
    return 2.0 + math.sqrt(max(0.0, SAIL_R ** 2 - xc * xc - (yc - 0.6) ** 2))

def sail_slab(pc):
    """Solid vault over the well: soffit = sphere r 0.85 at (0, 0.6, 2.0) clipped to the square, skirt over the walls, flat top z 3.0."""
    xs = [-1.0, -0.6] + [-0.45 + 0.15 * i for i in range(7)] + [0.6, 1.0]
    ys = [0.0] + [0.15 * i for i in range(1, 8)] + [1.2, 1.6]
    bm = bmesh.new()
    vb = [[bm.verts.new((x, y, sail_z(x, y))) for x in xs] for y in ys]
    vt = [[bm.verts.new((x, y, 3.0)) for x in xs] for y in ys]
    nx, ny = len(xs), len(ys)
    for j in range(ny - 1):
        for i in range(nx - 1):
            bm.faces.new((vb[j][i], vb[j][i + 1], vb[j + 1][i + 1], vb[j + 1][i]))
            bm.faces.new((vt[j][i], vt[j][i + 1], vt[j + 1][i + 1], vt[j + 1][i]))
    for i in range(nx - 1):
        bm.faces.new((vb[0][i], vb[0][i + 1], vt[0][i + 1], vt[0][i]))
        bm.faces.new((vb[ny - 1][i], vb[ny - 1][i + 1], vt[ny - 1][i + 1], vt[ny - 1][i]))
    for j in range(ny - 1):
        bm.faces.new((vb[j][0], vb[j + 1][0], vt[j + 1][0], vt[j][0]))
        bm.faces.new((vb[j][nx - 1], vb[j + 1][nx - 1], vt[j + 1][nx - 1], vt[j][nx - 1]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces[:])
    solid(pc, bm, lambda f: 'vault' if f.normal.z < -0.3 else 'mortar', cham=0.0)

def p_stair_turn():
    """Right-hand winder quarter turn: enters heading +y at the origin, exits heading +x at (0.6, 0.6)."""
    pc = FK.Piece('StairTurn'); R = R_('sturn')
    ext = lambda t: ((lambda s: (0.6 - s * math.cos(math.radians(t)), s * math.sin(math.radians(t))))(
        min(1.22 / max(1e-6, math.cos(math.radians(t))), 1.22 / max(1e-6, math.sin(math.radians(t))))))
    for k in range(4):                                     # winders fanned about the inner corner C = (0.6, 0)
        ta, tb = 22.5 * k, 22.5 * (k + 1)
        ca = (0.6 - 0.08 * math.cos(math.radians(ta)), 0.08 * math.sin(math.radians(ta)))
        cb = (0.6 - 0.08 * math.cos(math.radians(tb)), 0.08 * math.sin(math.radians(tb)))
        pa, pb = ext(ta), ext(tb)
        zt = -S_RISE * (k + 1)
        solid(pc, prism_ax([ca, pa, pb, cb], 'z', zt - 0.6, zt), 'vault', cham=0.02)
    lathe(pc, [(0, -1.0), (0.12, -1.0), (0.12, 0.9), (0.16, 0.92), (0.16, 1.02), (0.12, 1.04), (0.12, 2.05), (0, 2.05)], T(0.6, 0.0, 0.0), sides=14, mat='vault', seed=3.0)   # newel
    box(pc, 'lime', (0.4, 1.2, 4.5), (-0.8, 0.6, 0.45), cham=0.025, wob=0.003, seed=1)           # outer wall (left)
    box(pc, 'brick', (2.0, 0.4, 4.5), (0.0, 1.4, 0.45), cham=0.025, wob=0.003, seed=2)            # outer wall (front)
    box(pc, 'lime', (0.4, 0.4, 4.5), (0.8, -0.2, 0.45), cham=0.025, wob=0.003, seed=3)           # corner pier
    rr = 0.64                                              # exit tympanum above the next flight's arch (centre y 0.6, springing z 0.7)
    arc = [(1.2 * i / 14, 0.7 + math.sqrt(rr * rr - (1.2 * i / 14 - 0.6) ** 2)) for i in range(15)]
    solid(pc, prism_ax(arc + [(1.2, 2.7), (0.0, 2.7)], 'x', 0.62, 1.0), 'lime', cham=0.025)
    arc_ring_x(pc, 'brick', 0.6, 0.7, 0.6, 0.85, 7, 0.56, 0.12, tag='stvous')                   # brick voussoir ring on the tympanum
    sail_slab(pc)
    # sail vault stonework: concentric courses on the dome, arch bands on the four wall lines
    for j, rho in enumerate((0.30, 0.50)):
        n = int(round(2 * math.pi * rho / 0.21))
        for kk in range(n):
            th = 2 * math.pi * (kk + 0.5 * (j % 2)) / n
            x, y = rho * math.cos(th), 0.6 + rho * math.sin(th)
            if abs(x) > 0.48 or y < 0.12 or y > 1.08:
                continue
            phi = math.asin(rho / SAIL_R)
            p = Vector((0, 0.6, 2.0)) + SAIL_R * Vector((math.sin(phi) * math.cos(th), math.sin(phi) * math.sin(th), math.cos(phi)))
            t = Vector((-math.sin(th), math.cos(th), 0)); s = Vector((math.cos(phi) * math.cos(th), math.cos(phi) * math.sin(th), -math.sin(phi)))
            nin = t.cross(s)
            rot = Matrix(((t.x, s.x, nin.x, 0), (t.y, s.y, nin.y, 0), (t.z, s.z, nin.z, 0), (0, 0, 0, 1)))
            box(pc, 'vault', (2 * rho * math.pi / n * 0.92, 0.15, 0.06), p + nin * 0.01, rot @ Rz(R.uniform(-1.5, 1.5)), cham=0.014, wob=0.004, seed=R.random() * 90)
    arc_ring(pc, 'brick', 0, 2.0, 0.55, 0.85, 9, 0.075, 0.15, tag='stband')                     # entry face band
    arc_ring(pc, 'brick', 0, 2.0, 0.58, 0.85, 9, 1.125, 0.15, tag='stband2')                    # front wall band
    arc_ring_x(pc, 'brick', 0.6, 2.0, 0.58, 0.85, 9, -0.525, 0.15, tag='stbandl')               # left wall band
    arc_ring_x(pc, 'brick', 0.6, 2.0, 0.58, 0.85, 9, 0.545, 0.15, tag='stbandr')                # right (tympanum) band
    return pc

def p_stair_door():
    """The heroes' home cellar door and top landing; origin = start of the first flight. Landing y -1.2..0, door wall y -1.6..-1.2, house floor y -3.6..-1.6."""
    pc = FK.Piece('StairDoor'); R = R_('sdoor')
    flags(pc, R, -0.62, 0.62, -1.2, 0.0, 2)                                                    # landing floor
    flags(pc, R, -1.6, 1.6, -3.6, -1.6, 4)                                                     # house floor
    for sx in (-1, 1):                                                                         # landing walls up to the springing
        box(pc, 'lime', (0.4, 1.2, 1.9), (sx * 0.8, -0.6, 0.75), cham=0.025, wob=0.003, seed=sx + 5)
    ar = [(0.64 * math.cos(math.pi * (1 - i / 16)), 1.7 + 0.64 * math.sin(math.pi * (1 - i / 16))) for i in range(1, 16)]
    solid(pc, prism_ax([(-1.0, 1.7), (-0.64, 1.7)] + ar + [(0.64, 1.7), (1.0, 1.7), (1.0, 3.2), (-1.0, 3.2)], 'y', -1.2, 0.0), 'mortar', cham=0.0)   # backing over the vault
    for i, yc in enumerate((-0.9, -0.3)):
        arc_ring(pc, 'vault', 0, 1.7, 0.6, 1.0, 11, yc, 0.6, keystone=0.04, cham=0.02, tag='sdr%d' % i)
    r = 0.5                                                                                    # door wall with the arched doorway (1.0 wide, springing 1.6)
    prof = ([(-1.6, -0.2), (-0.5, -0.2), (-0.5, 1.6)] + [(r * math.cos(math.pi * (1 - i / 16)), 1.6 + r * math.sin(math.pi * (1 - i / 16))) for i in range(1, 16)] +
            [(0.5, 1.6), (0.5, -0.2), (1.6, -0.2), (1.6, 3.2), (-1.6, 3.2)])
    solid(pc, prism_ax(prof, 'y', -1.6, -1.2), 'brick', cham=0.03)
    for sx in (-1, 1):                                                                         # vault-stone jambs and voussoirs on the house face
        for k in range(4):
            box(pc, 'vault', (0.22 + R.uniform(0, 0.04), 0.14, 0.38), (sx * (0.61 + R.uniform(0, 0.02)), -1.67, 0.2 + 0.4 * k), Rx(R.uniform(-0.6, 0.6)),
                cham=0.02, wob=0.004, seed=R.random() * 90)
    arc_ring(pc, 'vault', 0, 1.6, 0.5, 0.76, 7, -1.67, 0.14, keystone=0.05, cham=0.02, tag='sdvous')
    def worn(p):
        return Vector((0, 0, -0.02 * max(0.0, 1 - (p.x / 0.55) ** 2)))
    box(pc, 'vault', (1.08, 0.52, 0.24), (0.0, -1.4, -0.08), cham=0.03, segs=2, cuts=(3, 2, 0), bend=worn, wob=0.003, seed=4)   # worn threshold
    # oak plank door: 5 boards, arched top, iron strap hinges and a ring, hinged at x -0.5 on the house side, open 105 degrees toward -y
    M0 = Matrix.Translation(Vector((-0.5, -1.77, 0.0))) @ Rz(-105)
    for j in range(5):
        xc = 0.1 + 0.2 * j
        top = 1.6 + math.sqrt(max(0.0, 0.25 - (xc - 0.5) ** 2)) - 0.02
        h = top - 0.02
        FK.add_box(pc, 'oak', (0.194, 0.05, h), M0 @ Matrix.Translation(Vector((xc, 0, 0.02 + h / 2))) @ Rz(R.uniform(-0.4, 0.4)), cham=0.008, wob=0.003,
                   seed=R.random() * 90, long_axis=2, uv_world=False)
    for zc in (0.45, 1.35):                                                                    # ledges on the landing side
        FK.add_box(pc, 'oak', (0.93, 0.04, 0.13), M0 @ Matrix.Translation(Vector((0.5, 0.045, zc))), cham=0.01, wob=0.003, seed=zc * 10, long_axis=0, uv_world=False)
    for zc in (0.38, 1.28):                                                                    # iron strap hinges on the house side
        FK.add_box(pc, 'iron', (0.78, 0.014, 0.085), M0 @ Matrix.Translation(Vector((0.4, -0.032, zc))), cham=0.006, wob=0.001, seed=zc * 7)
        FK.add_box(pc, 'iron', (0.1, 0.02, 0.14), M0 @ Matrix.Translation(Vector((0.02, -0.032, zc))), cham=0.006, wob=0.001, seed=zc * 3)
        FK.add_box(pc, 'iron', (0.04, 0.05, 0.16), M0 @ Matrix.Translation(Vector((-0.005, -0.012, zc))), cham=0.006, wob=0.001, seed=zc * 5)
    FK.add_box(pc, 'iron', (0.1, 0.012, 0.1), M0 @ Matrix.Translation(Vector((0.5, -0.031, 1.05))), cham=0.005, wob=0.001, seed=9)
    ring_prof = [(0.075 + 0.014 * math.cos(2 * math.pi * a / 8), 0.014 * math.sin(2 * math.pi * a / 8)) for a in range(8)]
    lathe(pc, ring_prof, M0 @ Matrix.Translation(Vector((0.5, -0.058, 1.0))) @ Rx(90), sides=14, mat='iron', closed=True, seed=2.0)   # the ring
    return pc

STAIR_PIECES = [('StairFlight', p_stair_flight), ('StairTurn', p_stair_turn), ('StairDoor', p_stair_door)]

# ----------------------------------------------------------------------------- wear
LAMPS =[Vector((-2.3, 2.0, 2.3)), Vector((2.3, 4.0, 2.3))]
ROOT_SITES = [(-0.4, 3.2), (0.9, 5.0)]
WALLY = {'VaultSegment', 'EndWall', 'NicheWall', 'StairEntry', 'ArchRib', 'Pillar'}

def sstep(a, b, x):
    t = min(1.0, max(0.0, (x - a) / (b - a)))
    return t * t * (3 - 2 * t)

STAIR_NAMES = {'StairFlight', 'StairTurn', 'StairDoor'}     # deliberately not in WALLY: its tide line is absolute z and would soak a descending flight

def stair_wear(name, p, n, mname, nz, st, r, g, b):
    """Stair kit wear: polish on treads (centre line / winders' walking line), damp in the lower 0.4 m above each tread, joints, smoke, moss at wall feet."""
    stone = mname in ('CV_Brick', 'CV_VaultStone', 'CV_Mortar')
    if name == 'StairFlight':
        d = p.z - s_nose(p.y)
    elif name == 'StairTurn':
        ang = 90.0 if (p.x >= 0.6 and p.y > 0.0) else math.degrees(math.atan2(max(p.y, 0.0), 0.6 - p.x))
        d = p.z + S_RISE * (min(3, max(0, int(ang / 22.5))) + 1)
    else:
        d = p.z
    if abs(n.z) < 0.7 and 0.0 <= d < 0.4 and stone:
        r = max(r, 0.85 * (1 - d / 0.4) * (0.8 + 0.2 * min(1.0, nz + 0.6)))              # damp rises from the tread
        if mname == 'CV_Brick' and d < 0.3:
            g = max(g, 0.5 * max(0.0, nz * 2 - 0.15) * (1 - d / 0.3))                      # moss at the wall foot
    if mname == 'CV_Mortar':
        r = max(r, 0.55 + 0.2 * nz)                                                         # dark joints
    if name == 'StairFlight':
        if mname == 'CV_VaultStone' and n.z > 0.6 and d < 0.3:
            b = 0.95 * max(0.0, 1 - abs(p.x) / 0.62) ** 0.6 * (0.8 + 0.2 * nz); r = max(r, 0.2)
        if stone and d > 1.55:
            r = max(r, sstep(1.55, 2.5, d) * 0.7 * (0.7 + 0.5 * st))                        # smoke on the vault crown
    elif name == 'StairTurn':
        if mname == 'CV_VaultStone' and n.z > 0.6 and d < 0.3 and p.z < 0.0:
            rho = math.hypot(p.x - 0.6, p.y)
            b = 0.95 * math.exp(-((rho - 0.75) / 0.3) ** 2) * (0.8 + 0.2 * nz); r = max(r, 0.2)   # the walking line round the newel
        if stone and p.z > 2.3:
            r = max(r, sstep(2.3, 2.85, p.z) * 0.7 * (0.7 + 0.5 * st))
    else:
        if mname == 'CV_VaultStone':
            if n.z > 0.6 and p.z < 0.1 and -1.7 < p.y < -1.1:
                b = 0.95 * (0.8 + 0.2 * nz); r = max(r, 0.15)                               # worn threshold
            elif p.y < -1.55:
                b = max(b, 0.55 * (0.6 + 0.4 * nz))                                         # hand-polished ring and jambs
        if mname == 'CV_Flagstone' and n.z > 0.5:
            b = 0.7 * math.exp(-(p.x / 0.5) ** 2) * (0.7 + 0.3 * nz)
            r = max(r, 0.12 + 0.2 * max(0.0, nz))
        if mname == 'CV_Oak':
            b = 0.3 + 0.4 * math.exp(-((p.z - 1.05) / 0.25) ** 2); r = max(r, 0.2)
        if mname == 'CV_Iron':
            r = max(r, 0.3); b = 0.35
        if stone and p.z > 2.0 and -1.3 < p.y < 0.1:
            r = max(r, sstep(2.0, 2.4, p.z) * 0.6 * (0.7 + 0.5 * st))
    return r, g, b

def wear_at(name, p, n, mname, place=None):
    nz = noise.noise(p * 1.7)
    st = noise.noise(Vector((p.x * 3.1, p.y * 3.1, p.z * 0.5)))
    r = 0.04 + 0.07 * max(0.0, nz); g = 0.0; b = 0.0
    pw = place(p) if place else None
    if name in WALLY:
        h = 0.78 + 0.28 * nz + 0.12 * st
        if p.z < h:
            r = max(r, 0.9 - 0.2 * (p.z / h))                           # damp tide: dark wet courses up to a crisp line
        elif p.z < h + 0.14:
            b = max(b, 0.85 * (0.75 + 0.25 * nz))                     # pale salt bloom just above the line
        if h <= p.z < h + 1.1:
            r = max(r, 0.4 * max(0.0, st + 0.1) * (1 - (p.z - h) / 1.1))
        if p.z > 2.2:
            r = max(r, sstep(2.2, 4.1, p.z) * 0.6 * (0.7 + 0.5 * st))   # smoke on the vault
        if p.z < 0.36 and n.z > -0.2:
            g = 0.5 * max(0.0, nz * 2 - 0.15)
        if mname == 'CV_Limewash':
            r = max(r, 0.18 * max(0.0, nz + 0.4))
    if name == 'NicheWall':
        if mname == 'CV_VaultStone' and n.z > 0.5:
            b = 0.5 * (0.7 + 0.3 * nz)
        if mname == 'CV_Brick' or mname == 'CV_Limewash':
            for z0 in NICHE_Z:
                for cx in NICHE_X:
                    d = math.hypot((p.x - cx) / 0.6, (p.z - (z0 + 0.5)) / 0.75)
                    if 0.95 < d < 1.9 and p.z > z0 + 0.5:
                        r = max(r, 0.5 * (1 - (d - 0.95) / 0.95) * (0.7 + 0.4 * abs(st)))
        if p.y < -0.05 and mname in ('CV_Limewash', 'CV_Brick'):
            r = max(r, 0.35)
    if name == 'StairEntry' and mname == 'CV_VaultStone' and n.z > 0.6 and p.y < -0.45:
        b = 0.95 * max(0.0, 1 - abs(p.x) / 0.62) ** 0.6 * (0.8 + 0.2 * nz)
        r = max(r, 0.2)
    if name == 'StairEntry' and p.y < -0.45 and mname in ('CV_Brick',):
        r = max(r, 0.45)
    if name == 'FloorTile':
        r = 0.16 + 0.26 * max(0.0, nz + 0.2)
        if n.z > 0.5:
            b = 0.45 * max(0.0, nz + 0.35) * (1 - 0.5 * abs(p.x))
        g = 0.28 * max(0.0, nz * 2 - 0.4) if abs(p.x) > 0.7 or p.y < 0.3 or p.y > 1.7 else 0.0
    if name == 'ArchRib' and mname == 'CV_VaultStone' and p.z > 1.35 and n.z > 0.5:
        b = 0.5
    if name == 'Pillar' and p.z > 1.4 and n.z > 0.5:
        b = 0.5
    if name == 'LarderShelf':
        if mname == 'CV_Oak':
            b = 0.55 * max(0.0, n.z) * (0.7 + 0.3 * nz) if n.z > 0.5 else 0.15 * max(0.0, 1 - abs(n.x))
            r = max(r, 0.25 + 0.3 * max(0.0, -n.z))
        if mname == 'CV_Iron':
            r = 0.2; b = 0.3
        if mname in ('CV_GlazeAmber', 'CV_GlazeCream', 'CV_Clay'):
            r = 0.1 + 0.25 * max(0.0, nz) + 0.35 * max(0.0, 1 - p.z % 0.56 * 6) * (n.z > 0.5)
            b = 0.3 * max(0.0, n.z)
    if name == 'CoalChute':
        if mname == 'CV_Oak':
            r = max(r, 0.55 + 0.2 * st)
            if n.z > 0.2 or abs(n.x) > 0.6:
                b = 0.6 * (0.6 + 0.4 * nz)
        if mname == 'CV_Iron':
            r = 0.45; b = 0.25 * max(0.0, n.z)
        if mname == 'CV_Soot':
            r = 0.0; b = 0.5 * max(0.0, n.z) * max(0.0, nz + 0.3)
    if name == 'BreathGrate' and mname == 'CV_Iron':
        r = 0.32 + 0.2 * max(0.0, nz)
        b = 0.7 * max(0.0, n.z) * (0.6 + 0.4 * nz)
    if name == 'Roots':
        r = 0.08 + 0.12 * max(0.0, nz) + 0.15 * (1 - sstep(-1.4, 0.0, p.z)); b = 0.45 + 0.2 * nz
        g = 0.12 * max(0.0, nz) * sstep(-0.5, 0.0, p.z)
    if name == 'Barrels':
        r = max(r, 0.5 * max(0.0, 1 - p.z / 0.28) + 0.18 * max(0.0, nz))
        if mname == 'CV_Iron':
            r = 0.3 + 0.2 * max(0.0, nz); b = 0.45 * max(0.0, n.z)
        if mname == 'CV_BarrelOak':
            d = math.hypot(p.x, p.y + 0.45 * (1 if p.y < 0 else -1) * 0)
            if n.z > 0.6:
                b = 0.3 * max(0.0, nz)
            if abs(n.x) > 0.8 and p.z < 0.7:
                r = max(r, 0.3)
    if name in STAIR_NAMES:
        r, g, b = stair_wear(name, p, n, mname, nz, st, r, g, b)
    # placed overlays (room mode): soot plumes over the lantern hooks, root stains under the crack
    if pw is not None and name in WALLY | {'FloorTile'} and mname in ('CV_Brick', 'CV_VaultStone', 'CV_Limewash', 'CV_Mortar'):
        for L in LAMPS:
            d = math.sqrt(((pw.y - L.y) / 1.4) ** 2 + ((pw.x - L.x) / 1.1) ** 2 + ((pw.z - L.z - 1.1) / 1.5) ** 2)
            if d < 1.0 and pw.z > L.z - 0.05:
                r = max(r, min(1.0, 1.35 * (1 - d) ** 0.5 * (0.8 + 0.2 * st)))
        for rx, ry in ROOT_SITES:
            d = math.sqrt(((pw.x - rx) / 1.0) ** 2 + ((pw.y - ry) / 0.8) ** 2)
            if pw.z > 2.6 and d < 1.0:
                r = max(r, 0.7 * (1 - d) * (0.55 + 0.9 * abs(st)))
    return min(1.0, r), min(1.0, g), min(1.0, b)

def write_wear(ob, place=None):
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
            attr.data[li].color = (*wear_at(ob.get('piece', ob.name), p, n, mname, place), 1.0)

# ----------------------------------------------------------------------------- build
def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def to_object(pc, parent):
    bmesh.ops.transform(pc.bm, matrix=Rz(180), verts=pc.bm.verts[:])
    me = bpy.data.meshes.new(pc.name)
    pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats:
        me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in SMOOTH:
            poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob['piece'] = pc.name
    ob.parent = parent
    bpy.context.view_layer.update()
    write_wear(ob)
    return ob

PIECES = [('VaultSegment', p_vault_segment), ('ArchRib', p_arch_rib), ('Pillar', p_pillar), ('EndWall', p_end_wall), ('NicheWall', p_niche_wall),
          ('StairEntry', p_stair_entry), ('FloorTile', p_floor_tile), ('CoalChute', p_coal_chute), ('LarderShelf', p_larder_shelf),
          ('BreathGrate', p_breath_grate), ('Roots', p_roots), ('Barrels', p_barrels)]

def build():
    root = bpy.data.objects.new('CellarVault', None); bpy.context.scene.collection.objects.link(root)
    obs = {}; total = 0
    for name, fn in PIECES:
        ob = to_object(fn(), root)
        obs[name] = ob; t = tris(ob); total += t
        print('  %-13s %6d tris  %2d slots  %s' % (name, t, len(ob.data.materials), ', '.join(m.name for m in ob.data.materials)))
    print('CellarVault total tris: %d (cap 60000, piece cap 12000)' % total)
    return root, obs

# ----------------------------------------------------------------------------- review scene
def W(x, y, z):
    """Unity coordinates -> Blender world (the kit is Rz(180) of the authoring frame, so Unity (x,y,z) = Blender (-x, -z, y))."""
    return Vector((-x, -z, y))

def instance(obs, name, ux, uy, uz, yaw=0.0, rewear=True):
    """Place a kit piece at Unity position (ux, uy, uz) with a Unity yaw in degrees about +Y (positive turns +Z toward +X).
    Unity (x, z) -> Blender (-x, -z) is a half turn, so a Unity yaw of t is a Blender rotation of -t about Z."""
    src = obs[name]
    ob = bpy.data.objects.new(name + '_i', src.data.copy() if rewear else src.data)
    ob['piece'] = name
    bpy.context.scene.collection.objects.link(ob)
    ob.location = W(ux, uy, uz); ob.rotation_euler = (0, 0, math.radians(-yaw))
    bpy.context.view_layer.update()
    if rewear:
        off = Vector((ux, uz, uy)); yr = math.radians(-yaw)
        Rm = Matrix.Rotation(yr, 3, 'Z')
        write_wear(ob, place=lambda p: off + Rm @ p)
    return ob

def light(sc, kind, name, loc, col, energy, **kw):
    ld = bpy.data.lights.new(name, kind); ld.color = col; ld.energy = energy
    for k, v in kw.items():
        setattr(ld, k, v)
    lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo); lo.location = loc
    return lo

def aim(lo, frm, to):
    lo.rotation_euler = (to - frm).to_track_quat('-Z', 'Y').to_euler()

def setup_render(sc):
    sc.render.engine = 'CYCLES'; sc.cycles.device = 'CPU'; sc.cycles.samples = SAMPLES; sc.cycles.use_denoising = True
    sc.cycles.max_bounces = 4; sc.cycles.caustics_reflective = False; sc.cycles.caustics_refractive = False
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 9 / 16)
    try:
        sc.view_settings.view_transform = 'Khronos PBR Neutral'
    except TypeError:
        sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = EXPO

def world_color(sc, col, strength):
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True
    bg = w.node_tree.nodes['Background']; bg.inputs['Color'].default_value = (*col, 1); bg.inputs['Strength'].default_value = strength

LAMP = float(FK.arg('--lamp', '3400')); SHAFT = float(FK.arg('--shaft', '0.5')); EXPO = float(FK.arg('--expo', '0.25'))
def build_room(obs):
    sc = bpy.context.scene
    for o in obs.values():
        o.hide_render = True; o.hide_viewport = True
    for iy in range(4):
        instance(obs, 'VaultSegment', 0, 0, 2 * iy)
        for ix in (-2, 0, 2):
            instance(obs, 'FloorTile', ix, 0, 2 * iy, rewear=False)
    for z in (0, 4, 8):
        instance(obs, 'ArchRib', 0, 0, z)
    instance(obs, 'StairEntry', 0, 0, 0)
    instance(obs, 'NicheWall', 0, 0, 8, yaw=180)
    instance(obs, 'LarderShelf', 0, 0, 1, rewear=False); instance(obs, 'LarderShelf', 0, 0, 3, rewear=False)
    instance(obs, 'CoalChute', 0, 0, 5.5, rewear=False)
    instance(obs, 'BreathGrate', 0.6, 0, 6.2, rewear=False)
    instance(obs, 'Roots', -0.4, 4.1, 3.2, rewear=False); instance(obs, 'Roots', 0.9, 4.1, 5.0, rewear=False)
    instance(obs, 'Barrels', -1.9, 0, 6.6, rewear=False)
    # lantern proxies (review only): iron cage + glowing core on wall hooks, warm point lights
    for ux, uy, uz in ((-2.3, 2.3, 2.0), (2.3, 2.3, 4.0)):
        loc = W(ux, uy, uz)
        bpy.ops.mesh.primitive_cube_add(size=1, location=loc); lm = bpy.context.object; lm.scale = (0.17, 0.17, 0.28)
        lm.data.materials.append(FK.flat_mat('CV_proxyIron', (0.02, 0.02, 0.02), 0.5))
        bpy.ops.mesh.primitive_uv_sphere_add(radius=0.09, location=loc); gl = bpy.context.object
        gl.data.materials.append(emissive('CV_proxyFlame', (1, 0.6, 0.2), (1.0, 0.70, 0.30), 70.0))
        light(sc, 'POINT', 'Lamp', loc + Vector((0, 0, 0)), (1.0, 0.74, 0.40), LAMP, shadow_soft_size=0.10)
    light(sc, 'POINT', 'Ember', W(0.6, 0.18, 6.2), (1.0, 0.30, 0.07), LAMP / 30, shadow_soft_size=0.12)
    hatch = W(2.25, 2.52, 5.5); foot = W(1.7, 0.4, 5.5)
    sp = light(sc, 'SPOT', 'Shaft', hatch, (0.96, 0.97, 1.0), LAMP * 5, spot_size=math.radians(20), spot_blend=0.5, shadow_soft_size=0.05)
    aim(sp, hatch, foot)
    # visible shaft: a cone of thin volume from the hatch to the floor (review only)
    bpy.ops.mesh.primitive_cone_add(vertices=24, radius1=1.0, radius2=0.3, depth=1.0, location=(0, 0, 0)); cone = bpy.context.object
    mid = (hatch + foot) / 2; ln = (foot - hatch).length
    cone.scale = (0.55, 0.55, ln * 1.02); cone.location = mid
    cone.rotation_euler = (foot - hatch).to_track_quat('Z', 'Y').to_euler()
    cm = bpy.data.materials.new('Shaft'); cm.use_nodes = True
    nt = cm.node_tree
    for n_ in list(nt.nodes):
        if n_.type == 'BSDF_PRINCIPLED':
            nt.nodes.remove(n_)
    vol = nt.nodes.new('ShaderNodeVolumePrincipled'); vol.inputs['Color'].default_value = (1, 0.97, 0.9, 1); vol.inputs['Density'].default_value = SHAFT
    vol.inputs['Anisotropy'].default_value = 0.4
    nt.links.new(vol.outputs[0], nt.nodes['Material Output'].inputs['Volume'])
    cone.data.materials.append(cm); cone.visible_shadow = False
    light(sc, 'POINT', 'Candle', W(-0.5, 0.9, 7.8), (1.0, 0.66, 0.28), LAMP / 30, shadow_soft_size=0.04)
    if '--fill' in argv:   # debug only: a neutral light in the middle of the room
        light(sc, 'POINT', 'DbgFill', W(0, 2.2, 4.0), (1, 1, 1), float(FK.arg('--fill', '3000')), shadow_soft_size=0.5)
    world_color(sc, (0.02, 0.013, 0.01), 1.0)

CAMS = {   # Unity (from, to, vertical fov)
    'wide': ((0.3, 1.7, 0.6), (0.0, 1.6, 8.0), 62),
    'mid': ((1.6, 1.6, 2.4), (-2.0, 1.2, 5.5), 62),
    'close': ((0.4, 1.3, 6.4), (-0.5, 1.4, 7.9), 55),
    'chute': ((-0.6, 1.5, 5.3), (2.2, 1.3, 5.5), 60),     # debug view
    'roofs': ((0.0, 1.2, 2.0), (0.2, 3.4, 4.2), 60),      # debug view
    'foot': ((-0.6, 1.2, 3.0), (2.5, 1.2, 4.2), 60),      # debug view
    'lampr': ((-1.0, 1.4, 2.4), (2.5, 2.8, 4.0), 60),     # debug view
}
LINEUPS = {   # name -> (pieces [(name, x centre, rotation about z in degrees)], cam from, cam to, fov), Blender coords
    'lineA': ([('StairEntry', -6.6, 0), ('NicheWall', 0.0, 0), ('EndWall', 6.6, 0)], (0.0, -11.8, 3.2), (0.0, 0, 2.2), 62),
    'lineB': ([('VaultSegment', -3.7, 0), ('ArchRib', 3.7, 0)], (0.0, -12.5, 3.4), (0.0, 0, 2.3), 52),
    'lineC': ([('LarderShelf', -3.4, 90), ('CoalChute', 0.0, -90), ('Barrels', 3.0, 0)], (0.0, -7.2, 1.9), (0.0, 0, 1.15), 58),
    'lineD': ([('Pillar', -2.3, 0), ('FloorTile', 0.2, 0), ('BreathGrate', 2.9, 0), ('Roots', 5.0, 0)], (1.5, -7.0, 2.3), (1.5, 0, 1.3), 58),
}

def look(cam, frm, to):
    cam.location = frm; cam.rotation_euler = (to - frm).to_track_quat('-Z', 'Y').to_euler()

def review(root, obs):
    sc = bpy.context.scene
    setup_render(sc)
    cam_d = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
    sc.camera = cam; cam_d.sensor_fit = 'VERTICAL'; cam_d.clip_start = 0.05
    done = []
    room = [v for v in VIEWS if v in CAMS]
    if room:
        build_room(obs)
        for v in room:
            frm, to, fov = CAMS[v]
            look(cam, W(*frm), W(*to)); cam_d.angle = math.radians(fov)
            sc.render.filepath = os.path.join(OUT, 'vault_%s.png' % v)
            bpy.ops.render.render(write_still=True); print('rendered', v)
    lines = [v for v in VIEWS if v in LINEUPS]
    if lines:
        for o in list(sc.objects):       # drop the room, keep the kit
            if o.type == 'LIGHT' or o.name.endswith('_i') or o.type == 'MESH' and o.name not in obs and not o.name.startswith('Cam'):
                bpy.data.objects.remove(o)
        for o in obs.values():
            o.hide_render = False; o.hide_viewport = False
        world_color(sc, (0.30, 0.26, 0.22), 1.0)
        sun = light(sc, 'SUN', 'Key', (0, 0, 10), (1.0, 0.85, 0.66), 3.2, angle=math.radians(4))
        sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
        fill = light(sc, 'SUN', 'Fill', (0, 0, 10), (0.9, 0.9, 1.0), 0.8, angle=math.radians(40))
        fill.rotation_euler = (math.radians(60), 0, math.radians(150))
        bpy.ops.mesh.primitive_plane_add(size=60, location=(0, 0, -0.07)); fl = bpy.context.object
        fl.data.materials.append(FK.flat_mat('CV_proxyGround', (0.12, 0.10, 0.085), 0.9))
        for v in lines:
            for o in obs.values():
                o.hide_render = True
            lst, frm, to, fov = LINEUPS[v]
            for name, x, rz in lst:
                ob = obs[name]; ob.hide_render = False; ob.rotation_euler = (0, 0, math.radians(rz)); ob.location = (0, 0, 0)
                bpy.context.view_layer.update()
                cs = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
                ob.location = (x - (min(c.x for c in cs) + max(c.x for c in cs)) / 2, -(min(c.y for c in cs) + max(c.y for c in cs)) / 2, 0)
            look(cam, Vector(frm), Vector(to)); cam_d.angle = math.radians(fov)
            sc.render.filepath = os.path.join(OUT, 'vault_%s.png' % v)
            bpy.ops.render.render(write_still=True); print('rendered', v)

# ----------------------------------------------------------------------------- stair build and review (--stair)
def build_stair():
    root = bpy.data.objects.new('CellarStair', None); bpy.context.scene.collection.objects.link(root)
    obs = {}; total = 0
    for name, fn in STAIR_PIECES:
        ob = to_object(fn(), root)
        obs[name] = ob; t = tris(ob); total += t
        cs = [Vector(c) for c in ob.bound_box]       # Blender local = Rz180 of the authoring frame; Unity (x, y, z) = (-bx, bz, -by)
        ext = (-max(c.x for c in cs), -min(c.x for c in cs), min(c.z for c in cs), max(c.z for c in cs), -max(c.y for c in cs), -min(c.y for c in cs))
        print('  %-11s %6d tris  %2d slots  %s' % (name, t, len(ob.data.materials), ', '.join(m.name for m in ob.data.materials)))
        print('              Unity extent x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f' % ext)
        if t > 8000:
            print('  !! %s over the 8000 tri piece cap' % name)
    print('CellarStair total tris: %d (cap 20000, piece cap 8000)' % total)
    return root, obs

SL = float(FK.arg('--sl', '0.12'))      # stair review light scale
STAIR_VIEWS = FK.arg('--views', 'top,turn,bottom' if '--room' in FK.argv else 'lineS').split(',')
CAMS_STAIR = {   # Unity (from, to, vertical fov), the contract's capture cameras (+ debug views)
    'dSlot': ((1.9, 6.6, -6.5), (0.9, 6.5, -7.15), 45),
    'dJ1': ((3.2, 9.6, -4.4), (3.3, 7.3, -6.3), 70),
    'dJ6': ((0.0, 4.1, -4.7), (0.0, 2.4, -3.3), 70),
    'dJ2': ((3.3, 8.0, -6.4), (2.2, 6.4, -6.6), 70),
    'dJ3': ((1.6, 6.5, -6.6), (0.5, 4.9, -6.5), 70),
    'dJ4': ((0.3, 5.4, -6.7), (0.0, 4.1, -5.6), 70),
    'dJ5': ((0.0, 4.7, -5.4), (0.0, 2.8, -3.0), 70),
    'dW': ((2.9, 9.0, -4.3), (3.8, 9.7, -5.3), 70),
    'dA': ((3.2, 10.2, -3.0), (3.2, 7.6, -6.0), 62),
    'dT1': ((3.4, 8.7, -5.5), (2.7, 6.8, -7.0), 62),
    'dB': ((3.0, 7.8, -6.6), (0.8, 5.6, -6.6), 62),
    'dT2': ((1.3, 6.3, -6.6), (-0.2, 4.4, -6.6), 62),
    'dC': ((0.0, 5.5, -6.4), (0.0, 3.2, -3.6), 62),
    'top': ((2.95, 10.65, -1.6), (3.2, 8.0, -5.6), 60),
    'turn': ((1.6, 7.4, -6.4), (-0.2, 4.5, -6.3), 62),
    'bottom': ((0.0, 3.6, -3.0), (0.2, 1.0, 5.0), 60),
}
STAIR_CHAIN = [('StairDoor', 3.2, 9.0, -4.0, 180), ('StairFlight', 3.2, 9.0, -4.0, 180), ('StairTurn', 3.2, 7.4, -6.0, 180),
               ('StairFlight', 2.6, 6.6, -6.6, -90), ('StairTurn', 0.6, 5.0, -6.6, -90), ('StairFlight', 0.0, 4.2, -6.0, 0)]

def build_stair_room(obs):
    sc = bpy.context.scene
    ent = to_object(p_stair_entry(), None)          # review only: the B5 StairEntry whose top step (0, 2.6, -4.0) the chain must reach
    obs = dict(obs); obs['StairEntry'] = ent
    for o in obs.values():
        o.hide_render = True; o.hide_viewport = True
    for name, ux, uy, uz, yaw in STAIR_CHAIN:
        instance(obs, name, ux, uy, uz, yaw, rewear=False)
    instance(obs, 'StairEntry', 0, 0, 0, 0, rewear=False)
    plug = bpy.data.objects.new('plug', bpy.data.meshes.new('plug')); sc.collection.objects.link(plug)
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts:
        v.co = Vector((v.co.x * 1.2, v.co.y * 0.15, v.co.z * 0.5))
    bm.to_mesh(plug.data); bm.free()
    plug.data.materials.append(FK.M('vault')); plug.location = W(0, 4.95, -3.975)
    # proxy lights (Unity coordinates)
    sp = W(3.2, 11.0, -1.0); tgt = W(3.2, 8.4, -4.8)
    s = light(sc, 'SPOT', 'House', sp, (0.82, 0.88, 1.0), LAMP * 6 * SL, spot_size=math.radians(60), spot_blend=0.6, shadow_soft_size=0.1); aim(s, sp, tgt)
    for loc, col, e, rad, name in ((W(3.2, 8.8, -7.05), (1.0, 0.66, 0.28), LAMP / 25 * SL, 0.03, 'Candle'),
                                   (W(-0.45, 6.4, -6.8), (1.0, 0.74, 0.40), LAMP * 0.8 * SL, 0.10, 'Lantern'),
                                   (W(0.0, 2.5, -2.5), (1.0, 0.62, 0.28), LAMP / 8 * SL, 0.15, 'Glow')):
        light(sc, 'POINT', name, loc, col, e, shadow_soft_size=rad)
    lm = W(-0.45, 6.4, -6.8)
    bpy.ops.mesh.primitive_uv_sphere_add(radius=0.07, location=lm); gl = bpy.context.object
    gl.data.materials.append(emissive('CV_proxyFlame', (1, 0.6, 0.2), (1.0, 0.70, 0.30), 70.0))
    world_color(sc, (0.05, 0.045, 0.04), 1.0)

def review_stair(root, obs):
    sc = bpy.context.scene
    setup_render(sc)
    cam_d = bpy.data.cameras.new('Cam'); cam = bpy.data.objects.new('Cam', cam_d); sc.collection.objects.link(cam)
    sc.camera = cam; cam_d.sensor_fit = 'VERTICAL'; cam_d.clip_start = 0.05
    room = [v for v in STAIR_VIEWS if v in CAMS_STAIR]
    if room:
        build_stair_room(obs)
        for v in room:
            frm, to, fov = CAMS_STAIR[v]
            look(cam, W(*frm), W(*to)); cam_d.angle = math.radians(fov)
            sc.render.filepath = os.path.join(OUT, 'stair_%s.png' % v)
            bpy.ops.render.render(write_still=True); print('rendered', v)
    if 'lineS' in STAIR_VIEWS:
        world_color(sc, (0.30, 0.26, 0.22), 1.0)
        sun = light(sc, 'SUN', 'Key', (0, 0, 10), (1.0, 0.85, 0.66), 3.2, angle=math.radians(4)); sun.rotation_euler = (math.radians(55), 0, math.radians(-35))
        for k, (name, x) in enumerate((('StairDoor', -5.5), ('StairFlight', 0.0), ('StairTurn', 4.5))):
            ob = obs[name]; ob.rotation_euler = (0, 0, 0); ob.location = (0, 0, 0)
            bpy.context.view_layer.update()
            cs = [ob.matrix_world @ Vector(c) for c in ob.bound_box]
            ob.location = (x - (min(c.x for c in cs) + max(c.x for c in cs)) / 2, -(min(c.y for c in cs) + max(c.y for c in cs)) / 2, 0)
        look(cam, Vector((0, -14, 5.5)), Vector((0, 0, 1.0))); cam_d.angle = math.radians(60)
        sc.render.filepath = os.path.join(OUT, 'stair_lineS.png')
        bpy.ops.render.render(write_still=True); print('rendered lineS')

def export_fbx(root, filename):
    ed = os.path.abspath(FK.arg('--export-dir', OUT)); os.makedirs(ed, exist_ok=True)
    for o in bpy.context.scene.objects:
        o.select_set(o == root or o.parent == root)
    bpy.ops.export_scene.fbx(filepath=os.path.join(ed, filename), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                             mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
    print('exported', os.path.join(ed, filename))

if __name__ == '__main__' and '--stair' in argv:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, obs = build_stair()
    if '--export' in argv:
        export_fbx(root, 'CellarStair.fbx')
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'stair.blend'))
    if '--no-render' not in argv:
        review_stair(root, obs)
    sys.exit(0)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, obs = build()
    if '--export' in argv:
        ed = os.path.abspath(FK.arg('--export-dir', OUT)); os.makedirs(ed, exist_ok=True)
        for o in bpy.context.scene.objects:
            o.select_set(o == root or o.parent == root)
        bpy.ops.export_scene.fbx(filepath=os.path.join(ed, 'CellarVault.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
        print('exported', os.path.join(ed, 'CellarVault.fbx'))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'vault.blend'))
    if '--no-render' not in argv:
        review(root, obs)
