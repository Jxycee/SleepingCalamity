"""Workshop: home kit family 4, the timber workshop with a lean-to (mass-production spec 5.4 #4; S and M classes).

A storybook open-fronted workshop: an oak post-and-plate frame clad in vertical boards (weathered, or painted from the
B4 palette, with battens on some) over a dressed sandstone plinth, gable to the street, a deep-eaved red tile or board
roof with a sagging ridge, rolled eave and rafter tails. The whole gable-end front is an OPEN BAY: two heavy posts, knee
braces and one big lintel beam over a worn stone threshold, so the street sees a workbench with a vise, a tool rack,
a grindstone, a sawhorse with a plank, a barrel and offcuts inside (a workshop, not a hole). A loft hatch with
its own shutters sits in the front gable under a hoist beam. A lean-to on one side (mono-pitch roof on posts, half-boarded
outer wall, split logs and a stack of seasoned planks under it, a chopping block with an axe on some). Windows with
shutters on the side and back walls, a stove pipe (A, C) or a sandstone stack (B). All four sides are framed and detailed
(spec 5.2); only roof backs and undersides are cheap.

Built from the B4 facade kit helpers (Art/Blender/town/sc_facade_kit.py, imported as a library: Piece, add_box, member,
window, kit_door, kit_shutters, wall_panel, plinth style, the shared B1 tileables and the linear `wear` colour
attribute). T2: no unique bake, metre UVs (UVMap, 2 m per tile on the 2K maps).

Variants (VARIANTS -> cfg(), seeded with random.Random(<seed>): width, depth, wall height, pitch, lean-to side and width,
palette, wear):
  Workshop_A  S class, tile roof, natural weathered boards, sage shutters, stove pipe, chopping block
  Workshop_B  M class, board roof, ochre-painted battened boards, oxblood shutters, sandstone stack on the back wall
  Workshop_C  M class, tile roof with a board lean-to roof, tarred dark boards, slate-blue shutters, stove pipe with a hood

Export (spec 5.2), one FBX per variant, root <Variant> with children:
  Frame          every piece except the parts, joined (multi-material, `wear` attribute): posts, plates, boards, roof, lean-to,
                 inside fittings; bare hinge pintles at the doorway, the hatch and every window
  Part_Door      the planked side-wall door (kit door: iron strap hinges, ring pull); object origin = SOCKET_Door
  Part_Shutters  every shutter (hatch, side and back windows); object origin = SOCKET_Shutters
  SOCKET_Door, SOCKET_Shutters   empties on the frame where the parts attach (SOCKET_Door marks the door side)
  FOOTPRINT      flat box (0.1 m) over the building body incl. the lean-to (and a stack): collider and NPC-block shape
The front is the OPEN BAY and never closes; Part_Door is the man door in the side wall AWAY from the lean-to, near the
front end. Without it the frame shows a dark-lined doorway with bare iron pintles and a worn step.
Axes: metres, Blender z up, front -Y; pivot = foundation centre at ground (body incl. lean-to centred on it). FBX -Z
forward, Y up, so the front comes in as Unity local +Z like the B4 facade.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_workshop.py -- --out <scratch>
      [--variant Workshop_A|Workshop_B|Workshop_C|all] [--export] [--no-render] [--views fl,fr,bl,br,front,in]
      [--rres 960] [--samples 32] [--frame-only]   (--frame-only: render with the parts hidden)
"""
import sys, os, math, random, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as fk

argv = fk.argv
arg = fk.arg
OUT = fk.OUT
rng = fk.rng
V3 = Vector

# the kit's timber members are long segments here (tris budget)
import inspect
_src = inspect.getsource(fk.member).replace('cuts = max(0, int(L / 0.6))', 'cuts = max(0, int(L / 1.5))')
exec(compile(_src, fk.__file__, 'exec'), fk.__dict__)

VARIANTS = {'Workshop_A': 0xA101, 'Workshop_B': 0xB202, 'Workshop_C': 0xC303}
PL = 0.45          # plinth height
PW = 0.26          # post section
PRD = fk.PROUD     # timbers stand this far proud of the board plane
TH = 0.22          # roof slab thickness (perpendicular)
CAP = {'S': (5.0, 6.0), 'M': (7.5, 8.0)}   # building body max, foundation

# ----------------------------------------------------------------------------- materials (WS_ slots + the kit's FK_ slots)
CUR = {}
EXTRA = {
    'board': lambda: fk.tile_mat('WS_Board', 'B1_town/oak_timber', tint=CUR['board'], normal=1.1),
    'boardpaint': lambda: fk.tile_mat('WS_BoardPaint', 'B1_town/painted_wood', paint=CUR['boardpaint'], normal=0.9),
    'roofboard': lambda: fk.tile_mat('WS_RoofBoard', 'B1_town/oak_timber', tint=CUR['roofboard'], normal=1.3),
    'floor': lambda: fk.tile_mat('WS_Floor', 'B1_town/sandstone', tint=(0.52, 0.48, 0.43)),
    'inner': lambda: fk.tile_mat('WS_Inner', 'B1_town/oak_timber', tint=(0.78, 0.70, 0.63)),
}
_orig_M = fk.M
fk.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

# ----------------------------------------------------------------------------- variation
def cfg(name):
    R = random.Random(VARIANTS[name])
    idx = list(VARIANTS).index(name)
    cls = 'S' if idx == 0 else 'M'
    if idx == 0:
        w, d, lw, zt = R.uniform(3.7, 3.88), R.uniform(4.0, 4.3), R.uniform(0.80, 0.85), R.uniform(2.82, 2.95)
    elif idx == 1:
        w, d, lw, zt = R.uniform(4.9, 5.1), R.uniform(5.5, 5.8), R.uniform(1.6, 1.75), R.uniform(3.1, 3.25)
    else:
        w, d, lw, zt = R.uniform(5.2, 5.4), R.uniform(5.0, 5.3), R.uniform(1.4, 1.55), R.uniform(3.0, 3.15)
    side = R.choice((-1, 1))
    style = [
        dict(board=(3.2, 2.9, 2.5), boardpaint=None, batten=False, roof='tile', leanroof='tile', chimney='pipe',
             paint='#4a6a48', block=True),
        dict(board=None, boardpaint=fk._lin('#9c7f4c'), batten=True, roof='board', leanroof='board', chimney='stack',
             paint='#6a2e29', block=False),
        dict(board=(1.75, 1.5, 1.28), boardpaint=None, batten=False, roof='tile', leanroof='board', chimney='pipe',
             paint='#3e5670', block=True),
    ][idx]
    return dict(name=name, idx=idx, cls=cls, w=round(w, 2), d=round(d, 2), lw=round(lw, 2), zt=round(zt, 2), side=side,
                pitch=R.uniform(40, 45), oe=R.uniform(0.52, 0.62), oel=0.28, ovF=0.65, ovB=0.42, sag=R.uniform(0.10, 0.15),
                lean=(R.choice((-1, 1)) * R.uniform(0.010, 0.016), R.uniform(0.006, 0.012)), wear=R.uniform(1.1, 1.35),
                paint=fk._lin(style['paint']), seed=VARIANTS[name], **{k: v for k, v in style.items() if k != 'paint'},
                paint_hex=style['paint'], door_t=R.uniform(1.15, 1.45), chim_x=R.uniform(-0.5, 0.5))

# ----------------------------------------------------------------------------- generic mesh helpers
def absorb(dst, src, xf):
    """Move a Piece's geometry, transformed by xf, into dst (material slots remapped)."""
    bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
    if xf.to_3x3().determinant() < 0:
        bmesh.ops.reverse_faces(src.bm, faces=src.bm.faces[:])
    me = bpy.data.meshes.new('tmp'); src.bm.to_mesh(me); src.bm.free()
    for m in src.mats:
        if m not in dst.mats:
            dst.mats.append(m)
    remap = [dst.mats.index(m) for m in src.mats]
    for p in me.polygons:
        p.material_index = remap[p.material_index] if p.material_index < len(remap) else 0
    dst.bm.from_mesh(me)
    bpy.data.meshes.remove(me)

def sheet(pc, mat, F, nu, nv, out, uvf):
    """Grid patch F(u, v) -> point; every quad is wound so its normal agrees with out(centre)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    Vs = [[bm.verts.new(F(i / nu, j / nv)) for i in range(nu + 1)] for j in range(nv + 1)]
    for j in range(nv):
        for i in range(nu):
            q = [Vs[j][i], Vs[j][i + 1], Vs[j + 1][i + 1], Vs[j + 1][i]]
            ctr = sum((v.co for v in q), Vector()) / 4
            nrm = (q[1].co - q[0].co).cross(q[3].co - q[0].co)
            if nrm.length < 1e-9:
                nrm = (q[2].co - q[1].co).cross(q[0].co - q[1].co)
            if nrm.dot(out(ctr)) < 0:
                q = q[::-1]
            try:
                f = bm.faces.new(q)
            except ValueError:
                continue
            for l in f.loops:
                l[uvl].uv = uvf(l.vert.co)
    bm.normal_update(); fk._merge(pc, bm, mat)

def poly(pc, mat, pts, out, uvf):
    """One planar polygon, wound toward out (a vector)."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    pts = [Vector(p) for p in pts]
    n = (pts[1] - pts[0]).cross(pts[2] - pts[0])
    if n.dot(out) < 0:
        pts = pts[::-1]
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    for l in f.loops:
        l[uvl].uv = uvf(l.vert.co)
    bm.normal_update(); fk._merge(pc, bm, mat)

def cyl(pc, mat, c0, c1, r0, r1, segs=8, cap=True, uvs=1.0):
    """Closed cone/cylinder from c0 to c1 (world), outward normals."""
    a = Vector(c0); b = Vector(c1); ax = (b - a); L = ax.length; ax.normalize()
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    bmesh.ops.create_cone(bm, cap_ends=cap, cap_tris=False, segments=segs, radius1=r0, radius2=r1, depth=L)
    q = Vector((0, 0, 1)).rotation_difference(ax).to_matrix().to_4x4()
    bm.transform(Matrix.Translation((a + b) / 2) @ q)
    for f in bm.faces:
        for l in f.loops:
            co = l.vert.co
            t = (co - a).dot(ax)
            l[uvl].uv = (t * uvs / fk.TILE * 3, (co.x + co.y) * uvs / fk.TILE * 3)
    bm.normal_update(); fk._merge(pc, bm, mat)

def mk(name):
    return fk.Piece(name)

FRAME_NAMES = ('Kit_Posts', 'Kit_Rails', 'Kit_Braces', 'Kit_Boards', 'Kit_Panels', 'Kit_Windows', 'Kit_Plinth')

# ----------------------------------------------------------------------------- geometry: walls (built in a wall frame)
# Wall frame: x along the wall (left to right seen from outside, centred), y = depth (0 = board plane, outward is -y,
# posts stand PRD proud), z up. xf_wall() puts it in the world.
def xf_wall(c, side):
    cx, w, d = c['cx'], c['w'], c['d']
    if side == 'front':
        return Matrix.Translation((cx, -d / 2, 0))
    if side == 'back':
        return Matrix.Translation((cx, d / 2, 0)) @ Matrix.Rotation(math.pi, 4, 'Z')
    if side == 'right':
        return Matrix.Translation((cx + w / 2, 0, 0)) @ Matrix.Rotation(math.pi / 2, 4, 'Z')
    return Matrix.Translation((cx - w / 2, 0, 0)) @ Matrix.Rotation(-math.pi / 2, 4, 'Z')

def post(P, x, z0, z1, name, wd=PW, dp=PW):
    fk.member(P['Kit_Posts'], 'oak', (x, -PRD + dp / 2, z0), (x, -PRD + dp / 2, z1), wd, dp, name=name)

def rail(P, xa, xb, zc, h, name, dp=PW, sag=0.0, ext=0.0, mat='oak'):
    fk.member(P['Kit_Rails'], mat, (xa, -PRD + dp / 2, zc), (xb, -PRD + dp / 2, zc), h, dp, name=name, sag=sag, extend=ext)

def brace(P, a, b, name, w=0.2):
    fk.member(P['Kit_Braces'], 'oak', (a[0], -PRD + PW / 2 + 0.015, a[1]), (b[0], -PRD + PW / 2 + 0.015, b[1]), w, PW - 0.03,
              name=name, cham=0.02)

def boards(P, xa, xb, z0, z1, holes, tag, mat, batten=False):
    """Vertical boards xa..xb x z0..z1 minus holes (x0, x1, z0, z1)."""
    R = rng('bd' + tag)
    n = max(1, round((xb - xa) / 0.21)); bw = (xb - xa) / n
    for i in range(n):
        bx0, bx1 = xa + i * bw, xa + (i + 1) * bw
        iv = [(z0, z1)]
        for (h0, h1, hz0, hz1) in holes:
            if h1 <= bx0 + 1e-6 or h0 >= bx1 - 1e-6:
                continue
            new = []
            for (a, b) in iv:
                if hz1 <= a or hz0 >= b:
                    new.append((a, b)); continue
                if hz0 > a:
                    new.append((a, hz0))
                if hz1 < b:
                    new.append((hz1, b))
            iv = new
        for (a, b) in iv:
            if b - a < 0.07:
                continue
            tj = R.uniform(-0.018, 0.0) if abs(b - z1) < 1e-6 else 0.0
            hh = b - a + tj
            dp = 0.045 + R.uniform(0.0, 0.012)
            fk.add_box(P['Kit_Boards'], mat, (bw - 0.012, dp, hh),
                       Matrix.Translation(((bx0 + bx1) / 2, -R.uniform(0.0, 0.012) + dp / 2, (a + b) / 2 + tj / 2)) @
                       Matrix.Rotation(math.radians(R.uniform(-0.6, 0.6)), 4, 'Y'),
                       cham=0.0, long_axis=2, seed=R.random() * 80)
    if batten:
        for i in range(1, n):
            xb_ = xa + i * bw
            if any(h0 - 0.02 < xb_ < h1 + 0.02 for (h0, h1, hz0, hz1) in holes):
                continue
            fk.add_box(P['Kit_Boards'], 'oak', (0.05, 0.022, z1 - z0 - 0.04), Matrix.Translation((xb_, -0.03, (z0 + z1) / 2)),
                       cham=0.0, long_axis=2, seed=i + 0.3)

def liner(P, L, zt, holes, zlo=0.0):
    """Dark lining behind the boards (outer face, seen through board gaps and openings) and the wall's inside face."""
    for plane, flip in ((0.20, False), (0.205, True)):
        tp = mk('t')
        fk.wall_panel(tp, 'inner', plane, -L / 2 + 0.1, L / 2 - 0.1, zlo, zt, holes=holes, bulge=0.0, cell=3.0)
        if flip:
            bmesh.ops.reverse_faces(tp.bm, faces=tp.bm.faces[:])
        absorb(P['Kit_Panels'], tp, Matrix.Identity(4))

def plinth_wall(P, xa, xb, tag, gaps=()):
    """Two courses of big chamfered sandstone over a dark mortar core, along a wall; gaps (doorways, the open bay) skipped."""
    runs = [(xa, xb)]
    for (g0, g1) in gaps:
        nr = []
        for (a, b) in runs:
            if g1 <= a or g0 >= b:
                nr.append((a, b)); continue
            if g0 > a:
                nr.append((a, g0))
            if g1 < b:
                nr.append((g1, b))
        runs = nr
    pc = P['Kit_Plinth']
    for ri, (a, b) in enumerate(runs):
        if b - a < 0.05:
            continue
        fk.add_box(pc, 'mortar', (b - a, 0.40, PL - 0.02), Matrix.Translation(((a + b) / 2, 0.10, (PL - 0.02) / 2)), cham=0.0, uv_world=True)
        R = rng('pl%s%d' % (tag, ri))
        for ci, (z0, z1) in enumerate(((0.0, 0.225), (0.225, PL))):
            x = a + (R.uniform(0.15, 0.4) if ci else 0.0)
            while x < b - 0.05:
                ln = min(R.uniform(0.7, 1.15), b - x)
                if b - (x + ln) < 0.3:
                    ln = b - x
                g = 0.03; h = z1 - z0 - g
                fk.add_box(pc, 'stone', (ln - g, 0.42 + R.uniform(-0.02, 0.03), h),
                           Matrix.Translation((x + ln / 2, 0.09 - R.uniform(0.0, 0.025), z0 + g / 2 + h / 2)) @
                           Matrix.Rotation(math.radians(R.uniform(-1.2, 1.2)), 4, 'Y'),
                           cham=0.045, segs=1, uv_world=True, wob=0.010, seed=R.random() * 90)
                x += ln

def window_item(P, shut, x0, x1, z0, z1, k, xs_bay):
    """A window with sill/head rails across its bay, glass in the reveal, bare hinge pintles; the shutters go to `shut`."""
    fk.window(P['Kit_Windows'], 0.0, x0, x1, z0, z1, k)
    rail(P, xs_bay[0], xs_bay[1], z0 - 0.08, 0.16, 'ws%d' % k)
    rail(P, xs_bay[0], xs_bay[1], z1 + 0.08, 0.16, 'wh%d' % k)
    pintles(P, x0, x1, z0, z1)
    shut.append((x0, x1, z0, z1, 0.0))

def pintles(P, x0, x1, z0, z1):
    for hx, sg in ((x0, -1), (x1, 1)):
        for zz in (z0 + 0.16, z1 - 0.16):
            fk.add_box(P['Kit_Windows'], 'iron', (0.05, 0.075, 0.04), Matrix.Translation((hx + sg * 0.012, -0.09, zz)), cham=0.006, seed=zz + hx)

def wall(P, c, side, L, items, tag, zb=PL):
    """Posts, plates, boards, braces and the openings of one wall. items: dicts kind 'door'|'win'|'open', xc, w (clear)."""
    zt = c['zt']
    mat = 'boardpaint' if c['boardpaint'] else 'board'
    post_x = []   # (x, width)
    holes = []
    shut = []
    door = None
    gaps = []
    ends = (L / 2 - 0.105, -(L / 2 - 0.105))
    for it in sorted(items, key=lambda i: i['xc']):
        xc, wd = it['xc'], it['w']
        if it['kind'] == 'door':
            d0, d1 = xc - wd / 2, xc + wd / 2
            post_x += [(d0 - 0.12, 0.24), (d1 + 0.12, 0.24)]
            door = (d0, d1, 0.16, it['z1'])
            holes.append((d0, d1, -1.0, it['z1']))
            gaps.append((d0 - 0.24, d1 + 0.24))
        elif it['kind'] == 'open':
            o0, o1 = xc - wd / 2, xc + wd / 2
            post_x += [(o0 - 0.16, 0.32), (o1 + 0.16, 0.32)]
            holes.append((o0, o1, -1.0, it['z1']))
            gaps.append((o0 - 0.32, o1 + 0.32))
        else:
            bx0, bx1 = xc - wd / 2 - 0.27, xc + wd / 2 + 0.27
            post_x += [(bx0, PW), (bx1, PW)]
    # even posts so no bay is wider than ~1.7
    xs = sorted([p[0] for p in post_x] + [-(L / 2 - 0.105), L / 2 - 0.105])
    fill = []
    for a, b in zip(xs, xs[1:]):
        inside_open = any(h[0] <= (a + b) / 2 <= h[1] for h in holes)
        if b - a > 1.75 and not inside_open:
            n = int(math.ceil((b - a) / 1.45))
            fill += [(a + (b - a) * k / n, PW) for k in range(1, n)]
    post_x += fill
    post_x.sort()
    # win holes
    for it in items:
        if it['kind'] == 'win':
            holes.append((it['xc'] - it['w'] / 2, it['xc'] + it['w'] / 2, it['z0'], it['z1']))
    zones = [(px - wd / 2 - 0.005, px + wd / 2 + 0.005, -9.0, 99.0) for px, wd in post_x]
    # posts (full height) and sole plates (broken at doors and the open bay)
    for k, (px, wd) in enumerate(post_x):
        post(P, px, 0.16 if any(g[0] <= px <= g[1] for g in gaps) else PL, zt - 0.02, '%s_p%d' % (tag, k), wd=wd)
    xa, xb = -L / 2 + 0.1, L / 2 - 0.1
    sole = [(xa, xb)]
    for (g0, g1) in gaps:
        ns = []
        for (a, b) in sole:
            if g1 <= a or g0 >= b:
                ns.append((a, b)); continue
            if g0 > a:
                ns.append((a, g0))
            if g1 < b:
                ns.append((g1, b))
        sole = ns
    for k, (a, b) in enumerate(sole):
        if b - a > 0.05:
            rail(P, a, b, PL + 0.1, 0.20, '%s_sole%d' % (tag, k), ext=0.02)
    rail(P, xa - 0.04, xb + 0.04, zt - 0.11, 0.24, '%s_plate' % tag, dp=PW + 0.04, sag=0.02)
    # boards between the posts
    boards(P, xa, xb, PL + 0.2, zt - 0.2, holes + zones, tag, mat, batten=c['batten'])
    liner(P, L, zt - 0.2, holes, zlo=PL + 0.2)
    # openings
    wi = 0
    for it in items:
        xc, wd = it['xc'], it['w']
        if it['kind'] == 'door':
            d0, d1 = xc - wd / 2, xc + wd / 2
            rail(P, d0 - 0.2, d1 + 0.2, it['z1'] + 0.12, 0.24, '%s_lintel' % tag, sag=0.012)
            for zz in (0.16 + 0.42, it['z1'] - 0.42):
                fk.add_box(P['Kit_Windows'], 'iron', (0.07, 0.05, 0.05), Matrix.Translation((d0 - 0.03, 0.06, zz)), cham=0.008, seed=zz + 1)
        elif it['kind'] == 'open':
            o0, o1 = xc - wd / 2, xc + wd / 2
            oh = it['z1']
            # the big lintel: a heavy carved beam over the bay, ends past the posts, sagging a little
            fk.member(P['Kit_Rails'], 'oak', (o0 - 0.42, -PRD - 0.06 + 0.2, oh + 0.22), (o1 + 0.42, -PRD - 0.06 + 0.2, oh + 0.22), 0.44, 0.40,
                      name='%s_lintel' % tag, sag=0.035, cham=0.035)
            for sg, ox in ((-1, o0), (1, o1)):   # knee braces to the lintel
                fk.member(P['Kit_Braces'], 'oak', (ox, -PRD + 0.12, oh - 0.85), (ox - sg * 0.62, -PRD + 0.12, oh - 0.02),
                          0.2, 0.2, name='%s_knee%d' % (tag, sg), cham=0.02)
            # infill boards above the lintel, up to the plate (already in `boards`: holes stop at oh)
        else:
            a, b = xc - wd / 2, xc + wd / 2
            bays = [p for p, _ in post_x]
            lo = max([p for p in bays if p < xc] or [xa]); hi = min([p for p in bays if p > xc] or [xb])
            window_item(P, shut, a, b, it['z0'], it['z1'], zlib.crc32((tag + str(wi)).encode()) % 1000, (lo, hi))
            wi += 1
    # braces: bays with no opening get a foot brace rising toward the wall's middle
    bays = [p for p, _ in post_x]
    for k, (a, b) in enumerate(zip(bays, bays[1:])):
        mid = (a + b) / 2
        if any(h[0] - 0.05 <= mid <= h[1] + 0.05 for h in holes) or b - a < 0.8:
            continue
        x0b, x1b = a + PW / 2, b - PW / 2
        ztb = PL + 0.75 * (zt - PL - 0.5)
        pa, pb = ((x0b, PL + 0.15), (x1b, ztb)) if mid < 0 else ((x1b, PL + 0.15), (x0b, ztb))
        brace(P, pa, pb, '%s_b%d' % (tag, k))
    plinth_wall(P, -L / 2 - 0.1, L / 2 + 0.1, tag, gaps=gaps)
    return shut, door, post_x, gaps

# ----------------------------------------------------------------------------- the roof (gable to the front, ridge along y)
class Roof:
    def __init__(self, c):
        self.c = c
        w, d, zt = c['w'], c['d'], c['zt']
        self.cx = c['cx']
        self.th = math.tan(math.radians(c['pitch']))
        self.zr0 = zt + 0.22 + (w / 2) * self.th
        self.y0 = -d / 2 - c['ovF']; self.y1 = d / 2 + c['ovB']
        self.L = self.y1 - self.y0; self.yc = (self.y0 + self.y1) / 2
        s = c['side']
        # eave half widths: the lean-to side tucks its eave in
        self.hw = {s: w / 2 + c['oel'], -s: w / 2 + c['oe']}
        self.sag = c['sag']

    def zridge(self, y):
        t = (y - self.yc) / (self.L / 2)
        return self.zr0 - self.sag * (1 - t * t)

    def top(self, sg, s, y):
        """Top of the sg (+1 / -1) slope; s 0 at the eave, 1 at the ridge."""
        HW = self.hw[sg]
        u = HW * (1 - s)
        t = (y - self.yc) / (self.L / 2)
        wave = 0.025 * noise.noise(Vector((y * 0.6, s * 2.5, sg * 0.37)))
        kick = 0.30 * max(0.0, 1 - s / 0.20) ** 2
        z = self.zridge(y) - u * self.th - 0.04 * (1 - t * t) * (1 - s) + wave * math.sin(math.pi * min(1.0, s)) + kick
        return Vector((self.cx + sg * u, y, z))

    def nrm(self, sg, s, y):
        a = self.top(sg, s, y); b = self.top(sg, min(1.0, s + 0.05), y); c_ = self.top(sg, s, y + 0.1)
        n = (c_ - a).cross(b - a)
        if n.z < 0:
            n = -n
        return n.normalized()

    def under_z(self, u, y):
        """Height of the slab underside at lateral offset u from the ridge (straight slope, for gable boards)."""
        return self.zridge(y) - abs(u) * self.th - TH / math.cos(math.atan(self.th)) + 0.03

def build_roof(c, R_, P, tile_mat_key):
    pc = P['Kit_Roof']
    cx = R_.cx
    mat = 'roof' if tile_mat_key == 'tile' else 'roofboard'
    nu = max(8, int(R_.L / 0.45))
    for sg in (-1, 1):
        cen = Vector((cx + sg * R_.hw[sg] * 0.5, R_.yc, R_.zr0 - 0.8))
        def Ft(u, v, sg=sg):
            y = R_.y0 + R_.L * u
            return R_.top(sg, v, y) - R_.nrm(sg, v, y) * 0.06
        def Fu(u, v, sg=sg):
            y = R_.y0 + R_.L * u
            return R_.top(sg, v, y) - R_.nrm(sg, v, y) * TH
        up = lambda p, sg=sg: Vector((sg * 0.7, 0, 1.0))
        down = lambda p, sg=sg: Vector((-sg * 0.5, 0, -1.0))
        sheet(pc, mat, Ft, nu, 10, up, lambda co: (co.y / fk.TILE, (co.x - cx) * sg / fk.TILE))
        sheet(pc, 'inner', Fu, nu, 4, down, lambda co: (co.y / fk.TILE, (co.x - cx) * sg / fk.TILE))
        # closing strips: the eave edge and both verge ends
        def Fe(u, v, sg=sg):
            y = R_.y0 + R_.L * u
            return R_.top(sg, 0, y) - R_.nrm(sg, 0, y) * (0.06 + (TH - 0.06) * v)
        sheet(pc, 'oak', Fe, nu, 1, lambda p, sg=sg: Vector((sg * 0.5, 0, -0.3)), lambda co: (co.y / fk.TILE, co.z / fk.TILE))
        for yy, oy in ((R_.y0, -1), (R_.y1, 1)):
            def Fv(u, v, sg=sg, yy=yy):
                return R_.top(sg, u, yy) - R_.nrm(sg, u, yy) * (0.06 + (TH - 0.06) * v)
            sheet(pc, 'oak', Fv, 8, 1, lambda p, oy=oy: Vector((0, oy, 0)), lambda co: ((co.x - cx) / fk.TILE, co.z / fk.TILE))
    # ridge cap
    nr = max(4, int(R_.L / 0.38)); rr = 0.15
    for i in range(nr):
        y = R_.y0 + (i + 0.5) * R_.L / nr
        Rn = rng('ridge%d%d' % (i, c['idx']))
        ln = R_.L / nr + 0.05
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        segs = 8 if tile_mat_key == 'tile' else 3
        rows = []
        zc = R_.zridge(y)
        for e in (-1, 1):
            row = []
            for k in range(segs + 1):
                a0 = math.pi * k / segs
                row.append(bm.verts.new(Vector((cx - rr * math.cos(a0) * (1.0 if e < 0 else 1.05), y + e * ln / 2, zc + rr * math.sin(a0) * 0.85 - 0.04))))
            rows.append(row)
        for k in range(segs):
            q = [rows[0][k], rows[1][k], rows[1][k + 1], rows[0][k + 1]]
            ctr = sum((v.co for v in q), Vector()) / 4
            n = (q[1].co - q[0].co).cross(q[3].co - q[0].co)
            if n.dot(ctr - Vector((cx, y, zc - 0.1))) < 0:
                q = q[::-1]
            f = bm.faces.new(q)
            for l in f.loops:
                l[uvl].uv = (l.vert.co.y / fk.TILE, math.atan2(l.vert.co.z - zc, l.vert.co.x - cx) * rr / fk.TILE)
        bmesh.ops.rotate(bm, verts=bm.verts[:], cent=Vector((cx, y, zc)), matrix=Matrix.Rotation(math.radians(Rn.uniform(-2.5, 2.5)), 3, 'Z'))
        bm.normal_update(); fk.two_sided(bm); fk._merge(pc, bm, mat)
    # courses: tile runs (about 21 cm) or long boards, butts standing proud
    for sg in (-1, 1):
        slope_len = (R_.top(sg, 1, R_.yc) - R_.top(sg, 0, R_.yc)).length
        course = 0.40 if tile_mat_key == 'tile' else 0.30
        nc = int(slope_len / course)
        run = 0.21 if tile_mat_key == 'tile' else 1.05
        nt = max(2, int(R_.L / run))
        for ci in range(nc):
            Rn = rng('course%d_%d_%d' % (ci, sg, c['idx']))
            s0, s1 = ci / nc, min(1.0, (ci + 1) / nc + 0.035)
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            for i in range(nt):
                ya = R_.y0 + R_.L * i / nt + 0.002; yb = R_.y0 + R_.L * (i + 1) / nt - 0.002
                j = Rn.uniform(-0.012, 0.012) if ci else Rn.uniform(-0.01, 0.045)
                if tile_mat_key != 'tile':
                    j = Rn.uniform(0.0, 0.02) + (0.012 if i % 2 else 0.0)
                rows = []
                for y, jj in ((ya, j), (yb, j + Rn.uniform(-0.006, 0.006))):
                    n_ = R_.nrm(sg, s0, y)
                    pb = R_.top(sg, s0, y) + n_ * (0.045 + jj)
                    rows.append((bm.verts.new(pb - n_ * 0.05), bm.verts.new(pb), bm.verts.new(R_.top(sg, s1, y))))
                a, b = rows
                up_s = (R_.top(sg, s1, R_.yc) - R_.top(sg, s0, R_.yc)).normalized()
                for q, outv in (((a[1], b[1], b[2], a[2]), R_.nrm(sg, s0, ya)), ((a[0], b[0], b[1], a[1]), -up_s + R_.nrm(sg, s0, ya) * 0.3)):
                    n = (q[1].co - q[0].co).cross(q[3].co - q[0].co)
                    if n.dot(outv) < 0:
                        q = q[::-1]
                    f = bm.faces.new(q)
                    for l in f.loops:
                        co = l.vert.co
                        l[uvl].uv = (co.y / fk.TILE, (co - R_.top(sg, s0, co.y)).dot(up_s) / fk.TILE + ci * course / fk.TILE + 0.03)
            bm.normal_update(); fk._merge(pc, bm, mat)
    # rolled eave lip (tile roofs): a fat half-round along each eave edge
    if tile_mat_key == 'tile':
        for sg in (-1, 1):
            segs = 6; rr_ = 0.10
            bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
            rows = []
            ny = max(8, int(R_.L / 0.5))
            for i in range(ny + 1):
                y = R_.y0 + R_.L * i / ny
                p0 = R_.top(sg, 0.0, y) + Vector((sg * 0.02, 0, -0.02))
                row = []
                for k in range(segs + 1):
                    a0 = math.pi * 0.15 + math.pi * 1.25 * k / segs
                    row.append(bm.verts.new(p0 + Vector((sg * math.cos(a0) * rr_, 0, math.sin(a0) * rr_))))
                rows.append(row)
            for i in range(ny):
                for k in range(segs):
                    q = [rows[i][k], rows[i + 1][k], rows[i + 1][k + 1], rows[i][k + 1]]
                    ctr = sum((v.co for v in q), Vector()) / 4
                    p0 = R_.top(sg, 0.0, ctr.y) + Vector((sg * 0.02, 0, -0.02))
                    n = (q[1].co - q[0].co).cross(q[3].co - q[0].co)
                    if n.dot(ctr - p0) < 0:
                        q = q[::-1]
                    f = bm.faces.new(q)
                    for l in f.loops:
                        l[uvl].uv = (l.vert.co.y / fk.TILE, k * rr_ / fk.TILE)
            bm.normal_update(); fk.two_sided(bm); fk._merge(pc, bm, mat)
    # rafter tails under the eaves, barge boards on both verges
    for sg in (-1, 1):
        nr_ = int((R_.L - 0.8) / 0.62)
        for i in range(nr_ + 1):
            y = R_.y0 + 0.4 + i * (R_.L - 0.8) / nr_
            n_ = R_.nrm(sg, 0.0, y)
            a = R_.top(sg, 0.03, y) - n_ * (TH + 0.09); b = R_.top(sg, 0.22 if sg == -c['side'] else 0.12, y) - n_ * (TH + 0.09)
            fk.member(P['Kit_Rails'], 'oak', a, b, 0.13, 0.18, name='raft%d_%d_%d' % (i, sg, c['idx']), cham=0.025, up=tuple(-n_))
        fa = R_.top(sg, 0, R_.y0) - R_.nrm(sg, 0, R_.y0) * 0.12; fb = R_.top(sg, 0, R_.y1) - R_.nrm(sg, 0, R_.y1) * 0.12
        fk.member(P['Kit_Rails'], 'oak', fa, fb, 0.20, 0.07, name='fascia%d%d' % (sg, c['idx']), up=(sg, 0, 0.5))
        for yy, nm in ((R_.y0 + 0.05, 'bf'), (R_.y1 - 0.05, 'bb')):
            a = R_.top(sg, 0, yy) - R_.nrm(sg, 0, yy) * 0.10 + Vector((0, 0.0, 0)); b = R_.top(sg, 1, yy) - R_.nrm(sg, 1, yy) * 0.04
            fk.member(P['Kit_Rails'], 'oak', a, b, 0.28, 0.08, name='%s%d%d' % (nm, sg, c['idx']), up=(0, -1 if yy < R_.yc else 1, 0), cham=0.02)

# ----------------------------------------------------------------------------- gables
def gable(P, c, R_, side, tag, hatch=None):
    """Framed gable triangle in the front/back wall frame (x centred, y 0 = board plane)."""
    w, zt = c['w'], c['zt']
    mat = 'boardpaint' if c['boardpaint'] else 'board'
    Rn = rng('gable' + tag)
    yw = -c['d'] / 2 if side == 'front' else c['d'] / 2
    yr = yw
    n = max(1, round(w / 0.21)); bw = w / n
    holes = []
    if hatch:
        holes.append(hatch)
    for i in range(n):
        bx0, bx1 = -w / 2 + i * bw, -w / 2 + (i + 1) * bw
        ztop = R_.under_z(min(abs(bx0), abs(bx1)), yr) - 0.02
        a, b = zt - 0.02, ztop
        if b - a < 0.08:
            continue
        iv = [(a, b)]
        for (h0, h1, hz0, hz1) in holes:
            if h1 <= bx0 + 1e-6 or h0 >= bx1 - 1e-6:
                continue
            new = []
            for (a_, b_) in iv:
                if hz1 <= a_ or hz0 >= b_:
                    new.append((a_, b_)); continue
                if hz0 > a_:
                    new.append((a_, hz0))
                if hz1 < b_:
                    new.append((hz1, b_))
            iv = new
        for (a_, b_) in iv:
            if b_ - a_ < 0.07:
                continue
            dp = 0.045 + Rn.uniform(0.0, 0.012)
            fk.add_box(P['Kit_Boards'], mat, (bw - 0.012, dp, b_ - a_),
                       Matrix.Translation(((bx0 + bx1) / 2, -Rn.uniform(0.0, 0.01) + dp / 2, (a_ + b_) / 2)) @
                       Matrix.Rotation(math.radians(Rn.uniform(-0.6, 0.6)), 4, 'Y'), cham=0.0, long_axis=2, seed=Rn.random() * 80)
    if c['batten']:
        for i in range(1, n):
            xb_ = -w / 2 + i * bw
            zz = R_.under_z(abs(xb_), yr) - 0.04
            if zz - zt < 0.2 or (hatch and hatch[0] - 0.05 < xb_ < hatch[1] + 0.05):
                continue
            fk.add_box(P['Kit_Boards'], 'oak', (0.05, 0.022, zz - zt), Matrix.Translation((xb_, -0.03, (zz + zt) / 2)), cham=0.0, long_axis=2, seed=i + 0.7)
    # dark backing under the gable, so a board gap shows dark
    zk = R_.under_z(0, yr) - 0.02
    poly(P['Kit_Panels'], 'inner', [(-w / 2, 0.2, zt - 0.02), (w / 2, 0.2, zt - 0.02), (0, 0.2, zk)], Vector((0, -1, 0)), lambda co: (co.x / fk.TILE, co.z / fk.TILE))
    poly(P['Kit_Panels'], 'inner', [(-w / 2, 0.205, zt - 0.02), (w / 2, 0.205, zt - 0.02), (0, 0.205, zk)], Vector((0, 1, 0)), lambda co: (co.x / fk.TILE, co.z / fk.TILE))
    # raking struts, collar
    zc = zt + 0.40 * (zk - zt)
    hc = (w / 2) * (1 - (zc - zt) / (zk - zt))
    if not hatch:
        fk.member(P['Kit_Rails'], 'oak', (-hc + 0.12, -PRD + PW / 2, zc), (hc - 0.12, -PRD + PW / 2, zc), 0.22, PW, name='collar' + tag)
    for sg in (-1, 1):
        fk.member(P['Kit_Braces'], 'oak', (sg * (w / 2 - 0.35), -PRD + PW / 2 + 0.015, zt), (sg * 0.72, -PRD + PW / 2 + 0.015, zc - 0.10), 0.2, PW - 0.03,
                  name='gs%s%d' % (tag, sg), cham=0.02)

# ----------------------------------------------------------------------------- the lean-to (built in the lean-side wall frame; outward = -y)
def lean_to(c, P, R_, side, wx_t):
    pc = P['Kit_Lean']; st = P['Kit_Stack']
    lw, zt, d = c['lw'], c['zt'], c['d']
    ta, tb = 0.95, d - 0.12      # distance from the front end
    xa, xb = sorted((wx_t(ta), wx_t(tb)))
    zh = zt - 0.48               # top of the lean roof at the main wall
    ov = 0.30
    slope = math.radians(14 if c['cls'] == 'S' else 17)
    zl = zh - (lw + ov) * math.tan(slope)
    Rn = rng('lean%d' % c['idx'])
    yo = -lw                     # outer post line
    pxs = [xa + 0.12, (xa + xb) / 2, xb - 0.12]
    vout = lw / (lw + ov)
    z_out_top = zh + 0.06 - (zh + 0.06 - zl) * vout
    zp = z_out_top - 0.26 - 0.14          # underside of the plate beam
    for k, x in enumerate(pxs):
        fk.member(pc, 'oak', (x, yo + 0.1, 0.0), (x, yo + 0.1, zp), 0.2, 0.2, name='lp%d_%d' % (k, c['idx']))
    fk.member(pc, 'oak', (xa - 0.12, yo + 0.1, zp + 0.11), (xb + 0.12, yo + 0.1, zp + 0.11), 0.22, 0.2, name='lplate%d' % c['idx'], sag=0.02)
    # roof
    mat = 'roof' if c['leanroof'] == 'tile' else 'roofboard'
    xr0, xr1 = xa - 0.18, xb + 0.18
    def top(u, v):   # u along x, v 0 at the main wall -> 1 at the eave
        return Vector((xr0 + (xr1 - xr0) * u, -(lw + ov) * v + 0.1, zh + 0.06 - (zh + 0.06 - zl) * v + 0.0))
    nrm = Vector((0, -math.sin(slope), math.cos(slope)))
    nrf = max(4, int((xb - xa) / 0.7))
    for k in range(nrf + 1):
        u = k / nrf * 0.9 + 0.05
        fk.member(pc, 'oak', top(u, 0.02) - nrm * 0.36, top(u, 0.96) - nrm * 0.36, 0.14, 0.15, name='lr%d_%d' % (k, c['idx']), up=(0, 0, 1), cham=0.02)
    sheet(pc, mat, lambda u, v: top(u, v), 8, 3, lambda p: nrm, lambda co: (co.x / fk.TILE, co.y / fk.TILE))
    sheet(pc, 'inner', lambda u, v: top(1 - u, v) - nrm * 0.26, 8, 2, lambda p: -nrm, lambda co: (co.x / fk.TILE, co.y / fk.TILE))
    # tile or board courses
    nco = 5
    tk = 0.21 if c['leanroof'] == 'tile' else 0.9
    ntl = max(3, int((xr1 - xr0) / tk))
    for ci in range(nco):
        v0, v1 = ci / nco, min(1.0, (ci + 1) / nco + 0.04)
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        for i in range(ntl):
            ua, ub = (i + 0.02) / ntl, (i + 0.98) / ntl
            j = Rn.uniform(-0.01, 0.015)
            pa = top(ua, v0) + nrm * (0.045 + j); pb = top(ub, v0) + nrm * (0.045 + j)
            qa = top(ua, v1); qb = top(ub, v1)
            for q, outv in (((bm.verts.new(pa), bm.verts.new(pb), bm.verts.new(qb), bm.verts.new(qa)), nrm),
                            ((bm.verts.new(pa - nrm * 0.05), bm.verts.new(pb - nrm * 0.05), bm.verts.new(pb), bm.verts.new(pa)), Vector((0, 1, -0.2)) + nrm * 0.3)):
                n = (q[1].co - q[0].co).cross(q[3].co - q[0].co)
                if n.dot(outv) < 0:
                    q = q[::-1]
                f = bm.faces.new(q)
                for l in f.loops:
                    l[uvl].uv = (l.vert.co.x / fk.TILE, l.vert.co.y / fk.TILE + 0.1 * ci)
        bm.normal_update(); fk._merge(pc, bm, mat)
    # ends of the slab
    for ue, ox in ((0.0, -1), (1.0, 1)):
        sheet(pc, 'oak', lambda uu, vv, ue=ue: top(ue, uu) - nrm * (0.06 + 0.20 * vv), 4, 1, lambda p, ox=ox: Vector((ox, 0, 0)),
              lambda co: (co.y / fk.TILE, co.z / fk.TILE))
    # fascia along the eave
    fk.member(pc, 'oak', Vector((xr0, -(lw + ov) + 0.1, zl + 0.0)) - nrm * 0.16, Vector((xr1, -(lw + ov) + 0.1, zl)) - nrm * 0.16, 0.30, 0.10,
              name='lfa%d' % c['idx'], up=(0, -1, 0.3))
    # the far end (towards the back): full board wall; the outer side: half-height boards with gaps; the front end open
    mat_b = 'boardpaint' if c['boardpaint'] else 'board'
    ybk = 0.0
    xe = xb + 0.06 if wx_t(tb) > wx_t(ta) else xa - 0.06
    sgn = 1 if wx_t(tb) > wx_t(ta) else -1
    nbk = max(3, int(lw / 0.2))
    for i in range(nbk):
        y = -(i + 0.5) * lw / nbk
        top_z = zh - (lw * (i + 0.5) / nbk / (lw + ov)) * (zh + 0.06 - zl) - 0.16
        hh = top_z - 0.45
        dp = 0.045
        fk.add_box(pc, mat_b, (dp, lw / nbk - 0.012, hh), Matrix.Translation((xe + sgn * 0.0, y, 0.45 + hh / 2)), cham=0.0, long_axis=2, seed=i + 2.0)
    nb = int((xb - xa) / 0.21)
    for i in range(nb):
        x = xa + (i + 0.5) * (xb - xa) / nb
        if Rn.random() < 0.18 or abs(x - pxs[1]) < 0.12:
            continue
        hh = 1.05 + Rn.uniform(-0.04, 0.04)
        fk.add_box(pc, mat_b, ((xb - xa) / nb - 0.012, 0.04, hh), Matrix.Translation((x, yo + 0.1 - 0.12, 0.05 + hh / 2)) @
                   Matrix.Rotation(math.radians(Rn.uniform(-1.2, 1.2)), 4, 'Y'), cham=0.0, long_axis=2, seed=Rn.random() * 70)
    # inside: split logs (end grain toward the front) and a plank stack, cleats between the planks
    half = (xa + xb) / 2
    xlog = (xa + 0.1, half - 0.12) if sgn > 0 else (half + 0.12, xb - 0.1)
    xpl = (half + 0.12, xb - 0.1) if sgn > 0 else (xa + 0.1, half - 0.12)
    nrow = 5 if c['cls'] == 'M' else 4
    for row in range(nrow):
        n_ = int((xlog[1] - xlog[0]) / 0.165) - (row % 2)
        for i in range(max(1, n_)):
            r = Rn.uniform(0.07, 0.095)
            x = xlog[0] + 0.08 + (i + 0.5 * (row % 2)) * 0.165 + Rn.uniform(-0.01, 0.01)
            if x > xlog[1]:
                continue
            z = 0.1 + row * 0.15
            cyl(st, 'oak_lt', (x, -0.10, z), (x, -(lw - 0.15), z), r, r * 0.95, segs=7, uvs=1.0)
    npl = 7 if c['cls'] == 'M' else 5
    for k in range(npl):
        z = 0.12 + k * 0.095
        wpl = (xpl[1] - xpl[0])
        fk.add_box(st, 'oak_lt', (wpl, lw - 0.3, 0.04), Matrix.Translation(((xpl[0] + xpl[1]) / 2 + Rn.uniform(-0.03, 0.03), -(lw) / 2 - 0.02 + Rn.uniform(-0.03, 0.03), z)) @
                   Matrix.Rotation(math.radians(Rn.uniform(-1, 1)), 4, 'Z'), cham=0.006, long_axis=1, seed=k * 3)
        if k % 2 == 0:
            for sx in (0.25, 0.5, 0.75):
                fk.add_box(st, 'oak', (0.04, lw - 0.3, 0.03), Matrix.Translation((xpl[0] + (xpl[1] - xpl[0]) * sx, -(lw) / 2 - 0.02, z + 0.04)), cham=0.0, seed=k + sx)
    return (xa, xb), zh

# ----------------------------------------------------------------------------- chimneys
def stove_pipe(P, c, R_, x, y, z_from, hood=False):
    pc = P['Kit_Chimney']
    sg = 1 if x > R_.cx else -1
    s = 1 - abs(x - R_.cx) / R_.hw[sg]
    ztop = R_.top(sg, s, y).z
    cyl(pc, 'iron', (x, y, z_from), (x, y, ztop + 1.0), 0.085, 0.085, segs=8)
    # flashing collar and a rain cap
    fk.add_box(pc, 'iron', (0.42, 0.42, 0.04), Matrix.Translation((x, y, ztop - 0.05)), cham=0.01, seed=x + y)
    cyl(pc, 'iron', (x, y, ztop + 0.45), (x, y, ztop + 0.52), 0.12, 0.12, segs=8)
    if hood:
        cyl(pc, 'iron', (x, y, ztop + 1.0), (x, y, ztop + 1.2), 0.22, 0.04, segs=8)
    else:
        cyl(pc, 'iron', (x, y, ztop + 1.0), (x, y, ztop + 1.12), 0.15, 0.02, segs=8)
    cyl(pc, 'iron', (x, y, ztop + 1.0 - 0.0), (x, y, ztop + 0.98), 0.12, 0.12, segs=8)

def stone_stack(P, c, R_, x, yb):
    """A sandstone stack against the back gable wall, shouldered, with a corbelled cap and two clay pots."""
    pc = P['Kit_Chimney']
    ztop = R_.zridge(yb) + 0.95
    h = ztop
    W_, D_ = 1.0, 0.70
    yc = yb + 0.45 - D_ / 2       # back face 0.2 proud of the wall plane
    def taper(p, h=h):
        t = (p.z + h / 2) / h
        return Vector((p.x * -0.05 * t, p.y * -0.05 * t, 0))
    fk.add_box(pc, 'stone_dk', (W_, D_, h), Matrix.Translation((x, yc, h / 2)), cham=0.035, cuts=(1, 1, int(h / 0.5)), bend=taper, wob=0.01, seed=21.0, uv_world=True)
    fk.add_box(pc, 'stone_dk', (W_ + 0.14, D_ + 0.14, 0.14), Matrix.Translation((x, yc, h - 0.02)), cham=0.03, seed=22.0, uv_world=True)
    fk.add_box(pc, 'stone_dk', (W_ + 0.04, D_ + 0.04, 0.12), Matrix.Translation((x, yc, h + 0.11)), cham=0.03, seed=23.0, uv_world=True)
    for k, (px, ph, tip) in enumerate(((-0.22, 0.52, 0.0), (0.22, 0.42, 4.0))):
        rings = []
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        prof = [(0.15, 0.0), (0.16, 0.05), (0.13, 0.12), (0.11, ph * 0.7), (0.12, ph - 0.06), (0.145, ph - 0.03), (0.145, ph), (0.10, ph)]
        segs = 10
        for (r, z) in prof:
            rings.append([bm.verts.new(Vector((r * math.cos(2 * math.pi * s_ / segs), r * math.sin(2 * math.pi * s_ / segs), z))) for s_ in range(segs)])
        for j in range(len(rings) - 1):
            for s_ in range(segs):
                f = bm.faces.new((rings[j][s_], rings[j][(s_ + 1) % segs], rings[j + 1][(s_ + 1) % segs], rings[j + 1][s_]))
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = (math.atan2(co.y, co.x) * 0.15 / fk.TILE, co.z / fk.TILE)
        bm.normal_update()
        bm.transform(Matrix.Translation((x + px, yc, h + 0.16)) @ Matrix.Rotation(math.radians(tip), 4, 'X'))
        fk._merge(pc, bm, 'roof')

# ----------------------------------------------------------------------------- inside fittings (world coordinates)
def inside(c, P, R_):
    pc = P['Kit_Inside']
    cx, w, d = c['cx'], c['w'], c['d']
    yB = d / 2
    Rn = rng('inside%d' % c['idx'])
    # floor slab and worn threshold across the open bay
    fk.add_box(pc, 'floor', (w - 0.5, d - 0.4, 0.16), Matrix.Translation((cx, 0.0, 0.08)), cham=0.01, uv_world=True)
    ow = c['ow']
    def dish(p):
        u = max(0.0, 1 - (p.x / (ow / 2 + 0.2)) ** 2); v = max(0.0, 1 - (p.y / 0.35) ** 2)
        return Vector((0, 0, -0.03 * u * v)) if p.z > 0 else Vector()
    fk.add_box(pc, 'stone', (ow + 0.7, 0.9, 0.17), Matrix.Translation((cx, -d / 2 + 0.05, 0.085)), cham=0.03, cuts=(6, 3, 0), bend=dish, wob=0.006, seed=3.3, uv_world=True)
    # workbench along the back wall: top, legs, lower shelf, vise
    bl = min(2.4, w - 1.3); bx = cx + c['bench_x']
    zt_ = 0.94
    fk.add_box(pc, 'oak_lt', (bl, 0.62, 0.08), Matrix.Translation((bx, yB - 0.20 - 0.40, zt_)), cham=0.012, long_axis=0, wob=0.004, seed=5.5)
    for sx in (-1, 1):
        for sy in (-0.24, 0.24):
            fk.member(pc, 'oak', (bx + sx * (bl / 2 - 0.12), yB - 0.60 + sy, 0.16), (bx + sx * (bl / 2 - 0.12), yB - 0.60 + sy, zt_ - 0.04), 0.10, 0.10, name='bl%d%f' % (sx, sy), cham=0.01)
    fk.add_box(pc, 'oak', (bl - 0.2, 0.56, 0.035), Matrix.Translation((bx, yB - 0.60, 0.34)), cham=0.008, long_axis=0, seed=6.5)
    fk.add_box(pc, 'oak', (0.14, 0.14, 0.12), Matrix.Translation((bx - bl / 2 + 0.2, yB - 0.30 - 0.38, zt_ + 0.1)), cham=0.01, seed=7.1)   # vise block
    fk.add_box(pc, 'iron', (0.05, 0.30, 0.05), Matrix.Translation((bx - bl / 2 + 0.2, yB - 0.60 - 0.12 + 0.0, zt_ + 0.08)), cham=0.005, seed=7.2)
    fk.add_box(pc, 'iron', (0.025, 0.025, 0.16), Matrix.Translation((bx - bl / 2 + 0.2, yB - 0.60 - 0.30, zt_ + 0.14)), cham=0.0, seed=7.3)
    # a plank in the vise, a half-finished stool leg, mallet, shavings (small chunky wedges)
    fk.add_box(pc, 'oak_lt', (0.5, 0.26, 0.04), Matrix.Translation((bx + 0.35, yB - 0.55, zt_ + 0.06)) @ Matrix.Rotation(math.radians(8), 4, 'Z'), cham=0.006, long_axis=0, seed=8.1)
    fk.add_box(pc, 'oak', (0.20, 0.07, 0.07), Matrix.Translation((bx + 0.05, yB - 0.50, zt_ + 0.075)) @ Matrix.Rotation(math.radians(-20), 4, 'Z'), cham=0.01, seed=8.4)
    fk.add_box(pc, 'oak', (0.05, 0.05, 0.22), Matrix.Translation((bx + 0.05 + 0.1, yB - 0.50 - 0.05, zt_ + 0.15)) @ Matrix.Rotation(math.radians(-20), 4, 'Z'), cham=0.008, seed=8.5)
    # tool rack on the back wall: a board with pegs and hanging tools (saw, hammer, tongs, planes, chisels)
    zr_ = 1.62
    fk.add_box(pc, 'oak', (bl - 0.1, 0.05, 0.14), Matrix.Translation((bx, yB - 0.22, zr_)), cham=0.01, long_axis=0, seed=9.1)
    for k in range(6):
        px = bx - (bl - 0.4) / 2 + k * (bl - 0.4) / 5
        fk.add_box(pc, 'oak', (0.035, 0.09, 0.035), Matrix.Translation((px, yB - 0.27, zr_ - 0.02)), cham=0.004, seed=9.2 + k)
    sx0 = bx - (bl - 0.4) / 2
    # hand saw: tapered blade and a handle
    poly(pc, 'iron', [(sx0 + 0.0, yB - 0.30, zr_ - 0.05), (sx0 + 0.06, yB - 0.30, zr_ - 0.05), (sx0 + 0.035, yB - 0.30, zr_ - 0.60), (sx0 + 0.0, yB - 0.30, zr_ - 0.55)], Vector((0, -1, 0)), lambda co: (co.x, co.z))
    fk.add_box(pc, 'oak_lt', (0.04, 0.03, 0.12), Matrix.Translation((sx0 + 0.03, yB - 0.30, zr_ - 0.02)), cham=0.006, seed=9.7)
    # hammers
    for k, dx in enumerate(((bl - 0.4) / 5 * 1.0, (bl - 0.4) / 5 * 2.0)):
        fk.add_box(pc, 'oak_lt', (0.035, 0.03, 0.30), Matrix.Translation((sx0 + dx, yB - 0.30, zr_ - 0.20)), cham=0.006, seed=9.8 + k)
        fk.add_box(pc, 'iron', (0.14, 0.05, 0.05), Matrix.Translation((sx0 + dx, yB - 0.30, zr_ - 0.05 + 0.0)), cham=0.008, seed=9.9 + k)
    # tongs (two thin bars in a V)
    for sg in (-1, 1):
        fk.add_box(pc, 'iron', (0.022, 0.02, 0.45), Matrix.Translation((sx0 + (bl - 0.4) / 5 * 3 + sg * 0.025, yB - 0.30, zr_ - 0.26)) @ Matrix.Rotation(math.radians(sg * 3), 4, 'Y'), cham=0.0, seed=sg + 10.0)
    # chisels and a wooden plane
    for k in range(3):
        fk.add_box(pc, 'iron', (0.02, 0.02, 0.22), Matrix.Translation((sx0 + (bl - 0.4) / 5 * 4 + k * 0.05, yB - 0.30, zr_ - 0.15)), cham=0.0, seed=k + 11.0)
        fk.add_box(pc, 'oak_lt', (0.03, 0.03, 0.09), Matrix.Translation((sx0 + (bl - 0.4) / 5 * 4 + k * 0.05, yB - 0.30, zr_ - 0.02)), cham=0.004, seed=k + 11.5)
    fk.add_box(pc, 'oak_lt', (0.30, 0.09, 0.09), Matrix.Translation((sx0 + (bl - 0.4) / 5 * 5, yB - 0.30, zt_ + 0.085)) @ Matrix.Rotation(math.radians(6), 4, 'Z'), cham=0.012, seed=12.0)
    # sawhorse with a plank across it, near the opening, off to the side
    sxh = cx - c['bench_x'] * 0.9 - 0.15
    syh = -d / 2 + 1.35
    for sx in (-1, 1):
        for sy in (-1, 1):
            fk.member(pc, 'oak', (sxh + sx * 0.38, syh + sy * 0.20, 0.0), (sxh + sx * 0.22, syh, 0.72), 0.07, 0.07, name='sh%d%d%d' % (sx, sy, c['idx']), cham=0.008)
    fk.add_box(pc, 'oak', (0.12, 0.08, 0.14), Matrix.Translation((sxh, syh, 0.78)), cham=0.01, seed=13.0)
    fk.add_box(pc, 'oak_lt', (1.8, 0.24, 0.045), Matrix.Translation((sxh, syh, 0.84)) @ Matrix.Rotation(math.radians(-14), 4, 'Z'), cham=0.008, long_axis=0, seed=13.2)
    # grindstone in a frame, against the side wall inside
    gx = cx + (c['side'] * -1) * (w / 2 - 0.9)
    gy = -d / 2 + 2.1 if d > 4.6 else -d / 2 + 2.3
    for sy in (-1, 1):
        fk.member(pc, 'oak', (gx, gy + sy * 0.20, 0.0), (gx, gy + sy * 0.20, 0.62), 0.09, 0.09, name='gs%d%d' % (sy, c['idx']), cham=0.01)
    cyl(pc, 'stone_dk', (gx, gy - 0.12, 0.62), (gx, gy + 0.12, 0.62), 0.30, 0.30, segs=12)
    fk.add_box(pc, 'iron', (0.025, 0.5, 0.025), Matrix.Translation((gx, gy, 0.62)), cham=0.0, seed=14.0)
    fk.add_box(pc, 'oak', (0.05, 0.05, 0.26), Matrix.Translation((gx + 0.28, gy + 0.22, 0.52)), cham=0.005, seed=14.2)
    # barrel and an offcut bin
    brx = cx - (1 if c['bench_x'] > 0 else -1) * (w / 2 - 0.75); bry = yB - 0.75
    cyl(pc, 'oak_lt', (brx, bry, 0.0), (brx, bry, 0.78), 0.30, 0.30, segs=12)
    for zz in (0.14, 0.62):
        cyl(pc, 'iron', (brx, bry, zz - 0.02), (brx, bry, zz + 0.02), 0.312, 0.312, segs=12)
    # offcuts and logs piled at the foot of the wall, a few shavings wedges
    for k in range(9):
        ox = cx + Rn.uniform(-w / 2 + 0.6, w / 2 - 0.6); oy = Rn.uniform(-d / 2 + 0.9, yB - 0.5)
        if abs(oy - (yB - 0.6)) < 0.5 and abs(ox - bx) < bl / 2 + 0.2:
            continue
        fk.add_box(pc, 'oak_lt', (Rn.uniform(0.2, 0.5), Rn.uniform(0.05, 0.12), 0.04), Matrix.Translation((ox, oy, 0.18 + 0.0)) @ Matrix.Rotation(Rn.uniform(0, 6.2), 4, 'Z'), cham=0.004, long_axis=0, seed=k + 15.0)
    # an iron stove (A, C): a drum on legs by the back corner, pipe handled by the roof
    # roof trusses inside: tie beams at the plate, king post and struts (seen through the open bay)
    for k, yy in enumerate((-d / 2 + 0.55, 0.0, d / 2 - 0.55)):
        zt = c['zt']
        fk.member(pc, 'oak', (cx - w / 2 + 0.1, yy, zt - 0.14), (cx + w / 2 - 0.1, yy, zt - 0.14), 0.24, 0.26, name='tie%d%d' % (k, c['idx']), up=(0, -1, 0), sag=0.03)
        zk = R_.zridge(yy) - 0.45
        fk.member(pc, 'oak', (cx, yy, zt - 0.14), (cx, yy, zk), 0.20, 0.22, name='king%d%d' % (k, c['idx']), up=(0, -1, 0))
        for sg in (-1, 1):
            fk.member(pc, 'oak', (cx + sg * 0.15, yy, zk - 0.2), (cx + sg * (w / 2 - 0.55), yy, zt - 0.14 + 0.25), 0.16, 0.2, name='st%d%d%d' % (k, sg, c['idx']), up=(0, -1, 0), cham=0.02)

# ----------------------------------------------------------------------------- build
def lean_xyz(p, c):
    lx, ly = c['lean']
    return Vector((p.x + lx * max(0.0, p.z - PL), p.y - ly * max(0.0, p.z - c['zt']), p.z))

def assemble(name):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    c = cfg(name)
    fk.MATS.clear(); fk.TIMBERS.clear()
    CUR['board'] = c['board'] or (1, 1, 1)
    CUR['boardpaint'] = c['boardpaint']
    CUR['roofboard'] = (1.05, 0.95, 0.85) if c['idx'] == 1 else (0.85, 0.78, 0.70)
    fk.OAK_TINT = (0.48, 0.45, 0.42)
    fk.SHUTTER_PAINT = fk.DOOR_PAINT = c['paint']
    s = c['side']
    c['cx'] = -s * c['lw'] / 2
    cx, w, d, zt = c['cx'], c['w'], c['d'], c['zt']
    R_ = Roof(c)
    names = FRAME_NAMES + ('Kit_Roof', 'Kit_Lean', 'Kit_Stack', 'Kit_Chimney', 'Kit_Inside', 'Kit_Shutters', 'Kit_Door')
    A = {n: mk(n) for n in names}
    shut_all = []
    door_info = None
    Rn = rng('layout%d' % c['idx'])
    # ---- front: the open bay (centred), flanking boards; the lintel carries the big beam
    ow = round(w - 1.95, 2)
    c['ow'] = ow
    oh = 2.12 if c['cls'] == 'S' else 2.42
    c['bench_x'] = Rn.choice((-1, 1)) * Rn.uniform(0.15, 0.4)
    for side in ('front', 'back', 'right', 'left'):
        xf = xf_wall(c, side)
        P = {n: mk(n) for n in FRAME_NAMES}
        L = w if side in ('front', 'back') else d
        sgn_t = 1 if side == 'right' else -1
        def wx(t, sgn_t=sgn_t):    # distance from the front end -> wall x (side walls)
            return sgn_t * (t - d / 2)
        items = []
        if side == 'front':
            items.append(dict(kind='open', xc=0.0, w=ow, z1=oh))
        elif side == 'back':
            for xw in ([0.0] if c['idx'] == 0 else [-1.5, 1.5] if c['idx'] == 1 else [-1.35, 1.35]):
                items.append(dict(kind='win', xc=xw, w=0.8, z0=1.25, z1=2.15))
        elif side == ('left' if s > 0 else 'right'):     # the free side
            items.append(dict(kind='door', xc=wx(c['door_t']), w=0.95, z1=2.12 if c['cls'] == 'S' else 2.18))
            for tw in ([2.95, 4.5] if d > 5.45 else [d * 0.68]):
                items.append(dict(kind='win', xc=wx(tw), w=0.8, z0=1.25, z1=2.15))
        else:
            pass   # the lean-to side wall: boards and braces only
        shut, door, post_x, gaps = wall(P, c, side, L, items, '%s%d' % (side, c['idx']))
        # gables on the front and back
        if side in ('front', 'back'):
            hatch = None
            if side == 'front':
                hz0 = zt + 0.30; hz1 = hz0 + (0.72 if c['cls'] == 'S' else 0.85); hw_ = 0.34 if c['cls'] == 'S' else 0.38
                hatch = (-hw_, hw_, hz0, hz1)
            gable(P, c, R_, side, '%s%d' % (side, c['idx']), hatch=hatch)
            if hatch:
                x0, x1, z0, z1 = hatch
                fk.member(P['Kit_Rails'], 'oak', (x0 - 0.1, -PRD + 0.07, z1 + 0.07), (x1 + 0.1, -PRD + 0.07, z1 + 0.07), 0.14, 0.14, name='hh%d' % c['idx'])
                fk.member(P['Kit_Rails'], 'oak', (x0 - 0.1, -PRD + 0.07, z0 - 0.06), (x1 + 0.1, -PRD + 0.07, z0 - 0.06), 0.14, 0.18, name='hs%d' % c['idx'], cham=0.02)
                for xx in (x0 - 0.04, x1 + 0.04):
                    fk.member(P['Kit_Posts'], 'oak', (xx, -PRD + 0.07, z0), (xx, -PRD + 0.07, z1), 0.10, 0.14, name='hp%d%f' % (c['idx'], xx))
                pintles(P, x0, x1, z0, z1)
                shut.append((x0, x1, z0, z1, 0.0))
                # a hoist beam over the hatch, with a hook
                fk.member(P['Kit_Rails'], 'oak', (0.0, -0.85, z1 + 0.42), (0.0, 0.25, z1 + 0.42), 0.14, 0.16, name='hoist%d' % c['idx'], up=(1, 0, 0), cham=0.02)
                fk.add_box(P['Kit_Windows'], 'iron', (0.03, 0.03, 0.22), Matrix.Translation((0.0, -0.80, z1 + 0.30)), cham=0.004, seed=3.1)
                fk.add_box(P['Kit_Windows'], 'iron', (0.10, 0.03, 0.03), Matrix.Translation((0.0, -0.80, z1 + 0.17)), cham=0.004, seed=3.2)
                fk.member(P['Kit_Braces'], 'oak', (0.0, -0.75, z1 + 0.37), (0.0, -0.05, z1 + 0.05), 0.10, 0.10, name='hoistb%d' % c['idx'], up=(1, 0, 0), cham=0.01)
        for n_, pc in P.items():
            if pc is not None:
                absorb(A[n_], pc, xf)
        for (a, b, z0, z1, plane) in shut:
            shut_all.append(((a, b, z0, z1, plane), xf))
        if door is not None:
            fk.DOOR = door
            dp = fk.kit_door()
            absorb(A['Kit_Door'], dp, xf)
            door_info = (door, xf)
    # shutters: each wall's set built in its frame
    byxf = {}
    for (win, xf) in shut_all:
        byxf.setdefault(tuple(map(tuple, xf)), (xf, []))[1].append(win)
    for key, (xf, wins) in byxf.items():
        sp = fk.kit_shutters(wins)
        absorb(A['Kit_Shutters'], sp, xf)
    # ---- corner posts (world): big, proud on both axes
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = cx + sx * (w / 2 - 0.105); y = sy * (d / 2 - 0.105)
            fk.add_box(A['Kit_Posts'], 'oak', (0.35, 0.35, zt - PL + 0.0), Matrix.Translation((x, y, PL + (zt - PL) / 2)), cham=0.03, long_axis=2, wob=0.004, seed=sx * 3 + sy + 40.0)
    # ---- roof
    build_roof(c, R_, A, c['roof'])
    # ---- the lean-to, in the lean side's wall frame
    lside = 'right' if s > 0 else 'left'
    xfl = xf_wall(c, lside)
    sgn_t = 1 if lside == 'right' else -1
    Pl = {n: mk(n) for n in ('Kit_Lean', 'Kit_Stack')}
    lean_to(c, Pl, R_, lside, lambda t: sgn_t * (t - d / 2))
    for n_, pc in Pl.items():
        absorb(A[n_], pc, xfl)
    # ---- chimney
    if c['chimney'] == 'pipe':
        px = cx - s * (w / 2 - 1.15); py = d / 2 - 1.15
        stove_pipe(A, c, R_, px, py, 0.86, hood=(c['idx'] == 2))
        # the drum stove under it
        fk.add_box(A['Kit_Inside'], 'iron', (0.5, 0.5, 0.62), Matrix.Translation((px, py, 0.55)), cham=0.04, seed=33.0)
        for sx in (-1, 1):
            for sy in (-1, 1):
                fk.add_box(A['Kit_Inside'], 'iron', (0.06, 0.06, 0.14), Matrix.Translation((px + sx * 0.18, py + sy * 0.18, 0.2)), cham=0.004, seed=33.1)
        c['stack'] = False
    else:
        stone_stack(A, c, R_, cx + c['chim_x'] * 0.6, d / 2)
        c['stack'] = True
    inside(c, A, R_)
    # ---- objects
    root = bpy.data.objects.new(name, None); bpy.context.scene.collection.objects.link(root)
    obs = []
    for n_ in names:
        pc = A[n_]
        if len(pc.bm.faces) == 0:
            pc.bm.free(); continue
        me = bpy.data.meshes.new(n_)
        smooth = n_ == 'Kit_Panels'
        if smooth:
            bmesh.ops.remove_doubles(pc.bm, verts=pc.bm.verts[:], dist=0.002)
        pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
        for m in pc.mats:
            me.materials.append(m)
        ob = bpy.data.objects.new(n_, me); bpy.context.scene.collection.objects.link(ob)
        for v in me.vertices:
            v.co = lean_xyz(v.co, c)
        for p in me.polygons:
            p.use_smooth = smooth
        if smooth:
            me.set_sharp_from_angle(angle=math.radians(30))
        obs.append(ob)
    door_world = None
    if door_info:
        (d0, d1, z0, z1), xfd = door_info
        door_world = lean_xyz(xfd @ Vector(((d0 + d1) / 2, 0.10, z0)), c)
        door_h = lean_xyz(xfd @ Vector((d1 - 0.17, 0.05, 1.10)), c)
    for ob in obs:
        write_wear(ob, c, R_, door_h if door_info else None)
    # ---- parts, sockets, footprint
    door = [o for o in obs if o.name == 'Kit_Door'][0]
    shutters = [o for o in obs if o.name == 'Kit_Shutters'][0]
    frame_obs = [o for o in obs if o not in (door, shutters)]
    # SOCKET_Shutters: the loft hatch centre in the front gable
    hz = zt + 0.30 + (0.36 if c['cls'] == 'S' else 0.425)
    sock_shut = lean_xyz(Vector((cx, -d / 2, hz)), c)
    def as_part(ob, nm, at):
        ob.name = nm; ob.data.name = nm
        ob.data.transform(Matrix.Translation(-at)); ob.location = at
        ob.parent = root
    as_part(door, 'Part_Door', door_world)
    as_part(shutters, 'Part_Shutters', sock_shut)
    bpy.ops.object.select_all(action='DESELECT')
    for o in frame_obs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = frame_obs[0]
    bpy.ops.object.join()
    frame = bpy.context.view_layer.objects.active
    frame.name = 'Frame'; frame.data.name = 'Frame'; frame.parent = root
    for nm, at in (('SOCKET_Door', door_world), ('SOCKET_Shutters', sock_shut)):
        e = bpy.data.objects.new(nm, None); e.empty_display_size = 0.3; e.location = at; e.parent = root
        bpy.context.scene.collection.objects.link(e)
    # footprint: body incl. the lean-to (and the stack), flat
    xfree = cx - s * (w / 2 + 0.07); xlean = cx + s * (w / 2 + c['lw'] + 0.07)
    x0, x1 = sorted((xfree, xlean))
    y0, y1 = -d / 2 - 0.07, d / 2 + (0.5 if c['stack'] else 0.07)
    fw, fd = x1 - x0, y1 - y0
    bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation(((x0 + x1) / 2, (y0 + y1) / 2, 0.05)) @ Matrix.Diagonal((fw, fd, 0.1, 1)))
    me = bpy.data.meshes.new('FOOTPRINT'); bm.to_mesh(me); bm.free()
    me.materials.append(bpy.data.materials.new('FOOTPRINT'))
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.hide_render = True
    tris = {o.name: sum(len(p.vertices) - 2 for p in o.data.polygons) for o in (frame, door, shutters)}
    total = sum(tris.values())
    extents = dict(x=tuple(sorted((cx - s * R_.hw[-s], cx + s * max(R_.hw[s], w / 2 + c['lw'] + 0.40)))), y=(R_.y0, R_.y1))
    cls_cap, found = CAP[c['cls']]
    print('SC WORKSHOP %s class=%s w=%.2f d=%.2f lw=%.2f zt=%.2f pitch=%.1f side=%+d footprint=%.2fx%.2f (cap %.1f) tris=%d (%s)' %
          (name, c['cls'], w, d, c['lw'], zt, c['pitch'], s, fw, fd, cls_cap, total, ', '.join('%s %d' % kv for kv in tris.items())))
    assert fw <= cls_cap and fd <= cls_cap, 'FOOTPRINT outside its class'
    assert abs(extents['x'][0]) <= found / 2 + 1e-6 and abs(extents['x'][1]) <= found / 2 + 1e-6 and abs(extents['y'][0]) <= found / 2 and abs(extents['y'][1]) <= found / 2, ('roof outside the foundation', extents)
    return root, frame, door, shutters, fp, c, dict(footprint=(fw, fd), tris=total, parts=tris, ridge=R_.zr0, roof_extents=extents)

# ----------------------------------------------------------------------------- wear (vertex colour, linear)
def write_wear(ob, c, R_, door_h):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    zt = c['zt']
    nm = ob.name
    mats = [m.name if m else '' for m in me.materials]
    amt = c['wear']
    for poly in me.polygons:
        n = poly.normal
        mname = mats[poly.material_index] if poly.material_index < len(mats) else ''
        for li in poly.loop_indices:
            p = me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.9); streak = noise.noise(Vector((p.x * 5.0, p.y * 5.0, p.z * 0.6)))
            r = max(0.0, 1 - p.z / (1.1 + 0.3 * nz + 0.2 * streak)) * 0.8            # splashback at the foot
            if zt - 0.7 < p.z < zt + 0.05:                                           # shade-damp band under the eave
                r = max(r, (p.z - (zt - 0.7)) / 0.7 * 0.3)
            r = max(r, 0.2 * max(0.0, noise.noise(p * 0.5 + Vector((3.1, 0.0, 1.7)))) + 0.07 * max(0.0, noise.noise(p * 2.5)))
            if nm in ('Kit_Boards',) or mname in ('WS_Board', 'WS_BoardPaint'):    # rain streaks down the boards, tide marks at the base
                r = max(r, 0.20 * max(0.0, noise.noise(Vector((p.x * 7.0, p.y * 7.0, p.z * 0.35))) - 0.05))
            g = 0.0
            b = 0.0
            if nm == 'Kit_Plinth' and n.z > 0.5:
                g = 0.7
            if nm == 'Kit_Plinth':
                r = max(r, 0.55 + 0.2 * streak)
            if nm in ('Kit_Roof', 'Kit_Lean') and n.z > 0.25 and mname in ('FK_RoofTile', 'WS_RoofBoard'):
                s_ = max(0.0, min(1.0, (R_.zr0 - p.z) / 2.0))                       # 1 at the eave
                g = max(0.0, s_ - 0.25) * 0.85 + max(0.0, 0.1 + 0.5 * nz) * 0.3 * s_
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.32 * tv * tv, 0.35 * s_ * (0.6 + 0.4 * streak))
                if nm == 'Kit_Lean':
                    g = max(g, 0.5 + 0.3 * nz)
            if nm == 'Kit_Chimney' and p.z > R_.zr0 - 0.2:
                r = max(r, min(1.0, (p.z - (R_.zr0 - 0.2)) / 1.0) * 0.8)
            if nm == 'Kit_Chimney' and n.z > 0.6:
                g = 0.55
            if nm in ('Kit_Inside', 'Kit_Stack') and mname == 'FK_OakDoor':
                b = 0.35 * max(0.0, n.z)                                              # sawn surfaces, worn pale
                r = max(r * 0.5, 0.15)
            if nm == 'Kit_Stack':
                r = max(r, 0.2)
            if nm in ('Kit_Shutters', 'Part_Door', 'Kit_Door'):
                ch = noise.noise(p * 9.0 + Vector((0.0, 7.0, 0.0)))
                r = max(r, min(0.6, max(0.0, ch - 0.22) * 3.0))
            if nm == 'Kit_Door' and door_h is not None:
                b = max(b, max(0.0, 1 - (Vector((p.x - door_h.x, p.y - door_h.y, (p.z - door_h.z) * 0.6))).length / 0.3) * 0.6)
                r = max(r, max(0.0, 1 - (p - door_h).length / 0.35) * 0.4)
            if nm in ('Kit_Posts', 'Kit_Rails', 'Kit_Braces') and n.z > 0.55:
                b = max(b, 0.30)
            if nm == 'Kit_Inside' and mname == 'FK_Sandstone' and n.z > 0.5:       # the worn threshold
                b = max(b, 0.4 * max(0.0, 1 - abs(p.x - c['cx']) / (c['ow'] / 2 + 0.2)))
            attr.data[li].color = (min(1.0, r * (amt if mname not in ('FK_Iron',) else 1.0)), min(1.0, g), min(1.0, b), 1.0)

# ----------------------------------------------------------------------------- review renders
VIEWS = {   # the four three-quarter corners (four turns: every side is seen), square-on, and a look into the open bay; scaled by class
    'fl': (Vector((-6.6, -8.4, 2.6)), Vector((0, 0, 2.0)), 46),
    'fr': (Vector((6.6, -8.4, 2.6)), Vector((0, 0, 2.0)), 46),
    'bl': (Vector((-6.6, 8.4, 2.6)), Vector((0, 0, 2.0)), 46),
    'br': (Vector((6.6, 8.4, 2.6)), Vector((0, 0, 2.0)), 46),
    'front': (Vector((0, -10.5, 1.8)), Vector((0, 0, 2.1)), 42),
    'in': (Vector((1.6, -5.6, 1.5)), Vector((0, 1.0, 1.3)), 58),
}

def render(name, views, c, suffix=''):
    sc = bpy.context.scene
    fk.RRES = int(arg('--rres', '960')); fk.SAMPLES = int(arg('--samples', '32'))
    cam = fk.review_scene(bpy.data.objects[name], variant=True)
    # review-only fill: stands in for the corner's bounce light inside the open bay (not exported, not parented)
    fl = bpy.data.lights.new('Fill', 'POINT'); fl.energy = 260; fl.color = (1.0, 0.82, 0.62); fl.shadow_soft_size = 0.4
    fo = bpy.data.objects.new('Fill', fl); sc.collection.objects.link(fo); fo.location = (c['cx'], -c['d'] / 2 + 1.4, 2.3)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(c['cx'] + 1.9, -c['d'] / 2 - 2.6, 0.875))
    fig = bpy.context.object; fig.name = 'Figure175'
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    fig.data.materials.append(fm)
    k = 1.0 if c['cls'] == 'S' else 1.3
    for v in views:
        frm, to, fov = VIEWS[v]
        if v != 'in':
            frm = Vector((frm.x * k, frm.y * k, frm.z)); 
        fig.hide_render = v != 'front'
        fk.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, '%s_%s%s.png' % (name, v, suffix))
        bpy.ops.render.render(write_still=True)
        print('rendered', sc.render.filepath)

def export(name, root):
    bpy.ops.object.select_all(action='DESELECT')
    root.select_set(True)
    for o in root.children:
        o.select_set(True)
    bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, name + '.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                             apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, mesh_smooth_type='FACE',
                             add_leaf_bones=False, colors_type='LINEAR', object_types={'EMPTY', 'MESH'},
                             path_mode='STRIP')   # no texture paths: Unity's material import renames every textured slot 'Lit' when an absolute path resolves

if __name__ == '__main__':
    which = arg('--variant', 'all')
    names = list(VARIANTS) if which == 'all' else [which]
    views = arg('--views', 'fl,fr,bl,br,front').split(',')
    for nm in names:
        root, frame, door, shut, fp, c, info = assemble(nm)
        if '--export' in argv:
            export(nm, root)
        if '--frame-only' in argv:
            door.hide_render = shut.hide_render = True
        if '--no-render' not in argv:
            render(nm, views, c, '_frame' if '--frame-only' in argv else '')
        with open(os.path.join(OUT, nm + '_info.txt'), 'w') as f:
            f.write(repr(dict(cfg={k: v for k, v in c.items() if k not in ('paint',)}, **info)) + '\n')
