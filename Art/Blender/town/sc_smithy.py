"""Smithy: Hollowmere's smithy (ring 2, M class, frame + parts), Q2 placement target.

A squat half-timbered workshop (cream plaster, dark adzed oak at about 1.4x real section, green shutters) on a sandstone
plinth under a steep HIPPED roof whose ridge runs front to back (so it never reads like the bakery's clipped front gable or
the facade's eave front). On the +X side the roof breaks over a flashed joint into a shallower catslide that shelters an open
forge bay: stone half walls with vertical boards above them at the back and the +X side, an open front, a flagged floor.
The hearth stands against the workshop's +X wall inside the bay, a soot-lined chimney breast carrying a tall square brick
stack through the catslide to about z 8 with iron stays back to the roof. The workshop has a wide plank double door (one
leaf ajar on a dark interior), a small shuttered window beside it, a loft hatch above with a swing crane arm and rope, a
lucky horseshoe, a weathervane. Yard (inside the +-4 m foundation): quench trough front right, tool rack on the -X wall, a
grindstone, stepping slabs, scrap and charcoal heap under the back eave, a coal bin in the bay.

Parts, each its own mesh with its pivot at its socket:
  Part_Forge  fire bed on the hearth slab (coals, the emissive BK_Ember glow, tuyere), the bellows beside it and a sheet-iron
              hood over the opening; without it the hearth is a cold, swept slab with a few ash smears under the stone breast.
  Part_Anvil  an anvil on its oak stump, a hammer and a bar; without it a charred ring and a ring of ash mark the spot.
  Part_Sign   a green board with a gilt anvil and an iron horseshoe, hanging by chains from the iron bracket; without it the
              bracket's two hooks hang empty.
SOCKET_Forge / SOCKET_Anvil / SOCKET_Sign are empties parented to Frame. FOOTPRINT is a flat box over the walls and bay.

Built from the bakery's helpers (sc_bakery imports sc_facade_kit, patches its materials and cheap members): timber_wall,
doorway, plinth_run, tube. Roof: a half-plane clipper builds every face (hips and the catslide) with courses of clay tile.
T2: no unique bake, shared tileables with metre UVs. Wear in the linear float attribute "wear" (R grime/soot, G moss,
B polish). BK_Ember is the only emissive slot.

refs: Reference/hollowmere/kit_ready/smithy/REFS.md; PLAN_smithy.md.

Axes (Blender): z up, front -Y, metres. Origin = foundation centre at ground (the M foundation is 8 x 8 m). Export FBX -Z
forward, Y up: the front (Blender -Y) is local +Z in Unity.

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_smithy.py -- --out <scratch>
      [--views tq,tql,back,frame,close,sign,anvil] [--rres 1000] [--samples 20] [--no-render] [--export] [--blend] [--tris]

Budget: under 35k tris.
"""
import sys, os, math, zlib
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK
import sc_bakery as BK        # patches FK.M / FK.member (cheap members) and holds the shared EXTRA materials

OUT = FK.OUT
argv = FK.argv
VIEWS = FK.arg('--views', 'tq,tql,back,frame,close').split(',')
RRES = int(FK.arg('--rres', '1000')); SAMPLES = int(FK.arg('--samples', '20'))
TILE = FK.TILE
TIM, PROUD = FK.TIM, FK.PROUD
PLINTH = FK.PLINTH

# ----------------------------------------------------------------------------- dimensions (metres)
X0, XW = -3.5, 0.5                 # workshop x range
XB = 3.5                           # bay +X wall
Y0, Y1 = -2.6, 2.8                 # front plane .. back
PLATE = 3.40
TH = 0.20                          # roof slab thickness
XE0, XE1, YE0, YE1 = -3.82, 3.78, -3.0, 3.2     # roof outline (eaves)
XR = -1.5                          # ridge x
SL, SF, S2 = 1.15, 1.45, 0.30      # workshop slope, hip slope, catslide slope
ZE = PLATE - SL * (X0 - XE0)        # eave z: the slope passes the plate top at the wall
RIDGE = ZE + SL * (XR - XE0)       # 5.70
STACK = (0.55, 1.50, 0.10, 1.10)   # x0, x1, y0, y1 of the shaft
STACK_TOP = 8.0
BREAST = (0.5, 1.45, -0.4, 1.6)    # chimney breast
OPEN = (0.0, 1.2, 0.85, 1.85)      # hearth opening on the breast's +X face: y0, y1, z0, z1
ANVIL_XY = (2.3, -0.6)
DOOR = (0.62, 2.22, 0.16, 2.15)    # front door, wall-local u, z
WIN = (2.80, 3.40, 0.95, 1.80)
HATCH = (2.80, 3.40, 2.45, 3.10)
L_FB, L_SIDE = XW - X0, Y1 - Y0

EXTRA = BK.EXTRA
EXTRA.update({
    'water': lambda: FK.flat_mat('BK_Water', (0.025, 0.04, 0.03), 0.06),
    'leather': lambda: FK.tile_mat('BK_Leather', 'B1_town/canvas', tint=(0.50, 0.30, 0.18)),
    'coal': lambda: FK.tile_mat('BK_Coal', 'B2_cellar/soot_coal', tint=(0.55, 0.55, 0.58), normal=1.3),
    'ash': lambda: FK.tile_mat('BK_Ash', 'B2_cellar/soot_coal', tint=(2.6, 2.5, 2.4), normal=0.4),
    'wheel': lambda: FK.tile_mat('BK_Grindstone', 'B1_town/sandstone', tint=(0.85, 0.85, 0.88)),
})

def T(x, y, z): return Matrix.Translation((x, y, z))
def Rz(a): return Matrix.Rotation(a, 4, 'Z')
def Rx(a): return Matrix.Rotation(a, 4, 'X')
def Ry(a): return Matrix.Rotation(a, 4, 'Y')

def piece(name): return FK.Piece(name)

def box(pc, mat, c, size, rot=None, cham=0.02, **kw):
    xf = T(*c) if rot is None else T(*c) @ rot
    FK.add_box(pc, mat, size, xf, cham=cham, **kw)

def poly(pc, mat, pts, hint, uvfn):
    """One n-gon face, wound to face `hint`."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    pts = [Vector(p) for p in pts]
    nrm = Vector()
    for i in range(1, len(pts) - 1):
        nrm += (pts[i] - pts[0]).cross(pts[i + 1] - pts[0])
    if nrm.dot(hint) < 0:
        pts = pts[::-1]
    f = bm.faces.new([bm.verts.new(p) for p in pts])
    for l in f.loops:
        l[uvl].uv = uvfn(l.vert.co)
    bm.normal_update(); FK._merge(pc, bm, mat)

def disc(pc, mat, c, r, h, seg=16, axis='Z', rot=None, wob=0.0, seed=0.0, uvoff=(0, 0)):
    """Capped cylinder centred at c, axis along Z (rotated by rot)."""
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=seg, radius1=r, radius2=r, depth=h)
    if wob:
        for v in bm.verts:
            q = v.co * 2.1 + Vector((seed, seed * 1.3, 0))
            k = 1 + wob * noise.noise(Vector((q.x, q.y, q.z)))
            v.co = Vector((v.co.x * k, v.co.y * k, v.co.z))
    xf = T(*c) @ (rot if rot is not None else Matrix.Identity(4))
    bm.transform(xf); bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, uvoff)
    FK._merge(pc, bm, mat)

def blob(pc, mat, c, size, seed, sub=1, flat=0.0, rot=None):
    """Lumpy icosphere: coal, ash, scrap, sacks."""
    bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=sub, radius=1.0)
    for v in bm.verts:
        k = 1 + 0.28 * noise.noise(v.co * 2.2 + Vector((seed, seed * 0.7, 0)))
        v.co = Vector((v.co.x * size[0] * k, v.co.y * size[1] * k, v.co.z * size[2] * k))
        if flat and v.co.z < 0:
            v.co.z *= flat
    bm.transform(T(*c) @ (rot if rot is not None else Matrix.Identity(4))); bm.normal_update()
    uvl = bm.loops.layers.uv.new('UVMap')
    FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (seed * 0.1, 0))
    FK._merge(pc, bm, mat)

# ----------------------------------------------------------------------------- roof
def zf(pl, x, y): return pl[0] + pl[1] * x + pl[2] * y

PLANES = {'L': (ZE - SL * XE0, SL, 0.0), 'R1': (RIDGE + SL * XR, -SL, 0.0), 'R2': (PLATE + S2 * XW, -S2, 0.0),
          'F': (ZE - SF * YE0, 0.0, SF), 'B': (ZE + SF * YE1, 0.0, -SF)}

def R2z(x): return zf(PLANES['R2'], x, 0)
def soffit(x): return R2z(x) - TH

def clip(pl, g):
    out = []
    n = len(pl)
    for i in range(n):
        a, b = pl[i], pl[(i + 1) % n]
        ga, gb = g(*a), g(*b)
        if ga >= 0:
            out.append(a)
        if (ga > 0 and gb < 0) or (ga < 0 and gb > 0):
            t = ga / (ga - gb)
            out.append((a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t))
    return out

def le(i, j):
    pi, pj = PLANES[i], PLANES[j]
    return lambda x, y: zf(pj, x, y) - zf(pi, x, y)
def xge(v): return lambda x, y: x - v
def xle(v): return lambda x, y: v - x

FACE_DEFS = [
    ('L', 'L', [xle(XR), le('L', 'F'), le('L', 'B')]),
    ('R1', 'R1', [xge(XR), xle(XW), le('R1', 'F'), le('R1', 'B')]),
    ('R2', 'R2', [xge(XW), le('R2', 'F'), le('R2', 'B')]),
    ('Fa', 'F', [xle(XR), le('F', 'L'), le('F', 'B')]),
    ('Fb', 'F', [xge(XR), xle(XW), le('F', 'R1'), le('F', 'B')]),
    ('Fc', 'F', [xge(XW), le('F', 'R2'), le('F', 'B')]),
    ('Ba', 'B', [xle(XR), le('B', 'L'), le('B', 'F')]),
    ('Bb', 'B', [xge(XR), xle(XW), le('B', 'R1'), le('B', 'F')]),
    ('Bc', 'B', [xge(XW), le('B', 'R2'), le('B', 'F')]),
]

def face_polys():
    res = []
    for name, pid, cons in FACE_DEFS:
        pl = [(XE0, YE0), (XE1, YE0), (XE1, YE1), (XE0, YE1)]
        for g in cons:
            pl = clip(pl, g)
            if len(pl) < 3:
                break
        if len(pl) < 3:
            continue
        ded = []
        for p in pl:
            if not ded or math.hypot(p[0] - ded[-1][0], p[1] - ded[-1][1]) > 1e-4:
                ded.append(p)
        if len(ded) > 1 and math.hypot(ded[0][0] - ded[-1][0], ded[0][1] - ded[-1][1]) < 1e-4:
            ded.pop()
        area = 0.0
        for i in range(len(ded)):
            a, b = ded[i], ded[(i + 1) % len(ded)]
            area += a[0] * b[1] - b[0] * a[1]
        if abs(area) / 2 < 0.03:
            continue
        pl3 = [Vector((x, y, zf(PLANES[pid], x, y))) for x, y in ded]
        res.append((name, pid, pl3))
    return res

def plane_frame(pid):
    a, b, c = PLANES[pid]
    n = Vector((-b, -c, 1.0)).normalized()
    u = Vector((b, c, b * b + c * c)).normalized()
    e = u.cross(n)
    return n, u, e

def on_outline(p):
    return [abs(p.x - XE0) < 1e-3, abs(p.x - XE1) < 1e-3, abs(p.y - YE0) < 1e-3, abs(p.y - YE1) < 1e-3]

OUTWARD = [Vector((-1, 0, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), Vector((0, 1, 0))]

def tile_face(pc, P3, n, u, e, tag, ds=0.42):
    """Courses of clay tile over a convex roof face; course lines at multiples of ds along the slope (shared across
    same-plane sub-faces)."""
    U = [p.dot(e) for p in P3]; S = [p.dot(u) for p in P3]
    smin, smax = min(S), max(S)
    P0, U0, S0 = P3[0], U[0], S[0]
    def chord(s):
        s = min(max(s, smin + 1e-4), smax - 1e-4)
        us = []
        for i in range(len(P3)):
            j = (i + 1) % len(P3)
            s0, s1 = S[i], S[j]
            if (s0 - s) * (s1 - s) <= 0 and abs(s1 - s0) > 1e-9:
                us.append(U[i] + (U[j] - U[i]) * (s - s0) / (s1 - s0))
        return min(us), max(us)
    def pt(uu, ss):
        return P0 + e * (uu - U0) + u * (ss - S0)
    for k in range(math.floor(smin / ds), math.ceil(smax / ds)):
        s0 = max(k * ds, smin); s1 = min((k + 1) * ds + 0.03, smax)
        if s1 - s0 < 0.09:
            continue
        ua0, ub0 = chord(s0); ua1, ub1 = chord(s1)
        a0, b0, a1, b1 = pt(ua0, s0), pt(ub0, s0), pt(ua1, s1), pt(ub1, s1)
        L = (b0 - a0).length
        if L < 0.12:
            continue
        R = FK.rng('c%s%d' % (tag, k))
        nt = max(2, int(L / 0.36))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        for i in range(nt):
            ua, ub = i / nt, (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if k else R.uniform(-0.008, 0.04)
            pts = []
            for uu in (ua + 0.002, ub - 0.002):
                pb = a0.lerp(b0, uu) + n * (0.045 + j)
                pt_ = a1.lerp(b1, uu)
                pts.append((bm.verts.new(pb - n * 0.05), bm.verts.new(pb), bm.verts.new(pt_)))
            A, B = pts
            for q in ((A[1], B[1], B[2], A[2]), (A[0], B[0], B[1], A[1])):
                f = bm.faces.new(q)
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = (co.dot(e) / TILE, co.dot(u) / TILE)
        bm.normal_update()
        FK._merge(pc, bm, 'roof')

def ridge_cap(pc, a, b, tag, big=False):
    ax = (b - a); L = ax.length; ax.normalize()
    side = ax.cross(Vector((0, 0, 1)))
    if side.length < 1e-3:
        side = Vector((1, 0, 0))
    side.normalize(); up = side.cross(ax).normalized()
    if up.z < 0:
        up = -up
    n = max(2, int(round(L / 0.42)))
    for i in range(n):
        R = FK.rng('rt%s%d' % (tag, i))
        c = a + ax * (L * (i + 0.5) / n)
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        segs = 8; r = 0.17 if big else 0.14; Lt = L / n + 0.05
        rows = []
        for e_ in (-1, 1):
            rr = r * (1.0 if e_ < 0 else 1.06)
            rows.append([bm.verts.new(c + ax * (e_ * Lt / 2) + side * (-rr * math.cos(math.pi * k / segs)) +
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

def roof():
    pc = piece('Roof')
    faces = face_polys()
    edges = {}
    for name, pid, P3 in faces:
        n, u, e = plane_frame(pid)
        uv = lambda c, e=e, u=u: (c.dot(e) / TILE, c.dot(u) / TILE)
        poly(pc, 'oak', [p - n * TH for p in P3], -n, uv)                 # soffit
        poly(pc, 'roof', [p - n * 0.06 for p in P3], n, uv)               # deck under the tile courses
        tile_face(pc, P3, n, u, e, name)
        for i in range(len(P3)):
            p, q = P3[i], P3[(i + 1) % len(P3)]
            key = tuple(sorted(((round(p.x, 3), round(p.y, 3), round(p.z, 3)), (round(q.x, 3), round(q.y, 3), round(q.z, 3)))))
            edges.setdefault(key, []).append((name, pid, p, q, n, u))
    sides = set()
    for key, lst in edges.items():
        name, pid, p, q, n, u = lst[0]
        L = (q - p).length
        if len(lst) == 1:
            op, oq = on_outline(p), on_outline(q)
            k = [i for i in range(4) if op[i] and oq[i]]
            if not k:
                continue
            out = OUTWARD[k[0]]
            # edge thickness skirt
            poly(pc, 'oak', [p, q, q - n * TH, p - n * TH], out, lambda c: (c.x / TILE + c.y / TILE, c.z / TILE))
            horiz = abs(p.z - q.z) < 0.02
            if horiz:
                FK.member(pc, 'oak', p - n * 0.13, q - n * 0.13, 0.22, 0.07, name='fa%s%d' % (name, k[0]), up=tuple(out))
                nt = max(2, int(L / 0.8))
                for t in range(nt):
                    c = p.lerp(q, (t + 0.5) / nt) - n * (TH + 0.07)
                    FK.member(pc, 'oak', c - u * 0.05, c + u * 0.62, 0.12, 0.16, name='rt%s%d' % (name, t), up=tuple(-n))
            else:
                a, b = p - n * 0.11 + out * 0.02, q - n * 0.11 + out * 0.02
                FK.member(pc, 'oak', a, b, 0.30, 0.08, name='bg%s%d' % (name, k[0]), up=tuple(out), wob=0.003)
        elif len(lst) == 2:
            ids = {lst[0][1], lst[1][1]}
            if len(ids) == 1:
                continue
            if ids == {'R1', 'R2'}:
                box(pc, 'iron', ((p.x + q.x) / 2, (p.y + q.y) / 2, PLATE + 0.03), (0.16, L, 0.045), cham=0.01, seed=L)
            else:
                ridge_cap(pc, p + Vector((0, 0, 0.07)), q + Vector((0, 0, 0.07)), '%s%.1f' % (name, p.x + p.y), big=ids == {'L', 'R1'})
    # exposed rafters under the catslide soffit, seen from the open bay
    n2, u2, e2 = plane_frame('R2')
    for k in range(9):
        y = Y0 + 0.15 + k * (L_SIDE - 0.3) / 8
        a = Vector((XW + 0.02, y, R2z(XW + 0.02))) - n2 * (TH + 0.07)
        b = Vector((XE1 - 0.12, y, R2z(XE1 - 0.12))) - n2 * (TH + 0.07)
        FK.member(pc, 'oak', a, b, 0.11, 0.15, name='rafb%d' % k, up=tuple(-n2))
    sag_roof(pc)
    return pc

def sag_roof(pc):
    """The ridge and the catslide settle a little: a smooth drop weighted by height and by distance from the hips."""
    for v in pc.bm.verts:
        p = v.co
        k = max(0.0, min(1.0, (p.z - 3.3) / 2.4))
        w = max(0.0, 1 - ((p.y - 0.1) / 3.3) ** 2)
        v.co.z -= 0.09 * k * w
        if p.x > XW:
            v.co.z -= 0.05 * max(0.0, 1 - ((p.y - 0.1) / 3.1) ** 2) * min(1.0, (p.x - XW) / 1.2) * max(0.0, 1 - (p.x - XW) / 3.4)

# ----------------------------------------------------------------------------- walls
F_FRONT = BK.face_xf((X0, Y0, 0), (0, -1, 0))
F_LEFT = BK.face_xf((X0, Y1, 0), (-1, 0, 0))
F_BACK = BK.face_xf((XW, Y1, 0), (0, 1, 0))
F_RIGHT = BK.face_xf((XW, Y0, 0), (1, 0, 0))

def plank_leaf(pc, xf, w, h, seed, np_=4, ring=True):
    """A closed planked leaf in leaf-local coords (x along the width from 0, z up), iron straps and a ring pull."""
    R = FK.rng('leaf%f' % seed)
    pw = w / np_
    for i in range(np_):
        box(pc, 'oak', (pw * (i + 0.5), 0.0, h / 2), (pw - 0.012, 0.05, h - 0.02), rot=Ry(math.radians(R.uniform(-0.6, 0.6))),
            cham=0.01, long_axis=2, wob=0.003, seed=R.random() * 30)
    for k, zz in enumerate((0.38, h - 0.38)):
        FK.add_box(pc, 'iron', (w * 0.96, 0.016, 0.085), xf @ T(w / 2, -0.032, zz), cham=0.0, long_axis=0, seed=zz + seed)
        for r_ in range(3):
            FK.add_box(pc, 'iron', (0.03, 0.02, 0.03), xf @ T(0.1 + r_ * (w - 0.2) / 2, -0.045, zz), cham=0.0, seed=r_ + zz)
    if ring:
        pts = [Vector((w - 0.12 + math.cos(a) * 0.045, -0.06, h * 0.5 + math.sin(a) * 0.045)) for a in [i * math.pi / 4 for i in range(9)]]
        BK.tube(pc, 'iron', [xf @ p for p in pts], 0.007, 4)

def put_leaf(pc, base, w, h, seed, np_=4, ring=True):
    """Build a plank leaf into a scratch piece and merge it transformed by base."""
    tmp = piece('leaf')
    plank_leaf(tmp, base, w, h, seed, np_, ring)
    # plank_leaf built the boxes untransformed (leaf-local): move them by base, iron already placed by base
    bm_l = []
    for v in tmp.bm.verts:
        pass
    return tmp

def leaf_into(pc, base, w, h, seed, np_=4, ring=True):
    """Planks in leaf-local coordinates placed by `base`; straps and ring placed by plank_leaf with the same base."""
    R = FK.rng('leaf%f' % seed)
    pw = w / np_
    for i in range(np_):
        FK.add_box(pc, 'oak', (pw - 0.012, 0.05, h - 0.02), base @ T(pw * (i + 0.5), 0.0, h / 2) @ Ry(math.radians(R.uniform(-0.6, 0.6))),
                   cham=0.01, long_axis=2, wob=0.003, seed=R.random() * 30)
    for zz in (0.38, h - 0.38):
        FK.add_box(pc, 'iron', (w * 0.96, 0.016, 0.085), base @ T(w / 2, -0.032, zz), cham=0.0, long_axis=0, seed=zz + seed)
        for r_ in range(3):
            FK.add_box(pc, 'iron', (0.03, 0.02, 0.03), base @ T(0.1 + r_ * (w - 0.2) / 2, -0.045, zz), cham=0.0, seed=r_ + zz)
    if ring:
        pts = [Vector((w - 0.12 + math.cos(a) * 0.045, -0.06, h * 0.5 + math.sin(a) * 0.045)) for a in [i * math.pi / 4 for i in range(9)]]
        BK.tube(pc, 'iron', [base @ p for p in pts], 0.007, 4)

def front_wall():
    du0, du1, dz0, dz1 = DOOR
    posts = [TIM / 2, du0 - TIM / 2, 2.51, 3.54, L_FB - TIM / 2]
    rails = [(PLINTH + 0.12, 0.24), (1.95, 0.20), (2.33, 0.20), (PLATE - 0.13, 0.26)]
    g = BK.timber_wall('FrontG', L_FB, PLINTH, PLATE, posts, rails,
                       opens=[('door', du0, du1, dz0, dz1), ('win', WIN[0], WIN[1], WIN[2], WIN[3]), ('shop', *HATCH)],
                       braces=[(TIM, 2.45, du0 + 0.1, PLATE - 0.15), (L_FB - TIM, 2.45, 3.54 + 0.12, PLATE - 0.15)], plinth=True)
    BK.doorway(g, du0, du1, dz0, dz1, depth=0.30, inner=1.6)
    # double door: left leaf closed, right leaf ajar on its hinge
    wl = (du1 - du0) / 2 - 0.02
    leaf_into(g, T(du0 + 0.01, 0.10, dz0), wl, dz1 - dz0 - 0.03, 1.0, np_=4, ring=False)
    leaf_into(g, T(du1 - 0.01, 0.10, dz0) @ Rz(math.radians(36)) @ T(-wl, 0, 0), wl, dz1 - dz0 - 0.03, 2.0, np_=4)
    # loft hatch: planked, recessed, with a dark interior behind
    u0, u1, z0, z1 = HATCH
    FK.quad_grid(g, 'interior', [(u0, 0.20, z0), (u1, 0.20, z0), (u1, 0.20, z1), (u0, 0.20, z1)], 1, 1, uv_fn=lambda c: (c.x, c.z))
    for (a, b, sgn) in ((u0, u1, 0),):
        for q in ([(u0, 0, z0), (u0, 0.2, z0), (u0, 0.2, z1), (u0, 0, z1)], [(u1, 0.2, z0), (u1, 0, z0), (u1, 0, z1), (u1, 0.2, z1)],
                  [(u1, 0, z1), (u0, 0, z1), (u0, 0.2, z1), (u1, 0.2, z1)]):
            FK.quad_grid(g, 'oak', q, 1, 1, uv_fn=lambda c: (c.x / TILE + c.y, c.z / TILE))
    leaf_into(g, T(u0 + 0.02, 0.07, z0 + 0.01), u1 - u0 - 0.04, z1 - z0 - 0.02, 3.0, np_=3, ring=False)
    FK.add_box(g, 'oak', (u1 - u0 + 0.26, 0.20, 0.07), T((u0 + u1) / 2, -0.05, z0 - 0.04), cham=0.015, long_axis=0, seed=5.5)
    # worn sandstone step, lucky horseshoe above the door
    FK.add_box(g, 'stone', (du1 - du0 + 0.55, 0.55, 0.16), T((du0 + du1) / 2, -0.28, 0.08) @ Ry(math.radians(1.0)),
               cham=0.035, segs=2, cuts=(3, 1, 0), uv_world=True, wob=0.01, seed=8.0)
    hs = [Vector((1.42 + math.sin(a) * 0.13, -0.06, 2.80 - math.cos(a) * 0.13)) for a in [-2.35 + i * 4.7 / 8 for i in range(9)]]
    hs = [Vector((1.42 - math.sin(a) * 0.13, -0.075, 2.78 + math.cos(a) * 0.13)) for a in [-2.35 + i * 4.7 / 8 for i in range(9)]]
    BK.tube(g, 'iron', hs, 0.020, 5)
    # swing crane arm out of the front, rope and hook
    ux = 3.54
    FK.add_box(g, 'oak', (0.14, 1.05, 0.15), T(ux, -0.52, 2.55), cham=0.025, long_axis=1, wob=0.004, seed=11.0)
    FK.member(g, 'oak', (ux, -0.05, 1.95), (ux, -0.82, 2.48), 0.12, 0.12, name='crane_brace', up=(1, 0, 0))
    disc(g, 'iron', (ux, -0.95, 2.43), 0.07, 0.05, 12, rot=Ry(math.pi / 2))
    FK.add_box(g, 'oak', (0.1, 0.16, 0.2), T(ux, -0.95, 2.42), cham=0.02, seed=12.0)
    for ry in (-0.90, -1.00):
        FK.add_box(g, 'sack', (0.025, 0.025, 1.45), T(ux, ry, 1.66), cham=0.0, seed=ry)
    for k in range(7):
        a0 = k / 6 * math.pi * 1.3
        FK.add_box(g, 'iron', (0.025, 0.025, 0.05), T(ux, -0.95 + math.sin(a0) * 0.05, 0.9 - (1 - math.cos(a0)) * 0.05) @ Rx(a0), cham=0.0, seed=k)
    # a bundle of bar stock hanging on the hook
    for k in range(4):
        FK.add_box(g, 'iron', (0.025, 0.025, 0.55), T(ux + (k - 1.5) * 0.03, -0.95, 0.62), cham=0.0, seed=k + 3)
    return g

def side_wall(tag, L, posts, win, braces, brace_up=True):
    rails = [(PLINTH + 0.12, 0.24), (1.95, 0.22), (PLATE - 0.13, 0.26)]
    opens = [('win', *win)] if win else []
    return BK.timber_wall(tag, L, PLINTH, PLATE, posts, rails, opens=opens, braces=braces, plinth=True)

def left_wall():
    return side_wall('Left', L_SIDE, [TIM / 2, 1.2, 2.16, 3.24, 4.3, L_SIDE - TIM / 2], (2.30, 3.10, 1.0, 1.85),
                     [(TIM, 0.85, 1.05, PLATE - 0.15), (L_SIDE - TIM, 0.85, 4.45, PLATE - 0.15)])

def back_wall():
    return side_wall('Back', L_FB, [TIM / 2, 1.16, 2.24, L_FB - TIM / 2], (1.30, 2.10, 1.2, 2.05),
                     [(TIM, 0.85, 1.0, PLATE - 0.15), (L_FB - TIM, 0.85, 2.4, PLATE - 0.15)])

def right_wall():
    """The hearth wall, inside the bay: a plank door to the workshop near the front, the breast hides the middle."""
    rails = [(PLINTH + 0.12, 0.24), (1.95, 0.22), (PLATE - 0.13, 0.26)]
    g = BK.timber_wall('Right', L_SIDE, PLINTH, PLATE, [TIM / 2, 0.41, 1.54, 2.0, 4.4, L_SIDE - TIM / 2], rails,
                       opens=[('door', 0.55, 1.40, 0.16, 2.10)],
                       braces=[(L_SIDE - TIM, 0.85, 4.5, PLATE - 0.15), (2.0, 2.4, 1.6, PLATE - 0.15)], plinth=True)
    BK.doorway(g, 0.55, 1.40, 0.16, 2.10, depth=0.30, inner=1.2)
    leaf_into(g, T(0.57, 0.10, 0.16), 0.81, 1.92, 4.0, np_=4)
    return g

# ----------------------------------------------------------------------------- the bay
def stone_course(pc, x0, x1, z0, z1, seed, depth=0.40):
    """Wall-local stone course at y ~ 0.14 (like plinth_run) between z0 and z1."""
    R = FK.rng('sc%f' % seed)
    x = x0
    while x < x1 - 0.05:
        ln = min(R.uniform(0.8, 1.3), x1 - x)
        if x1 - (x + ln) < 0.4:
            ln = x1 - x
        g = 0.035
        FK.add_box(pc, 'stone', (ln - g, depth + R.uniform(-0.02, 0.03), z1 - z0 - g),
                   T(x + ln / 2, 0.14 - R.uniform(0.0, 0.03), (z0 + z1) / 2) @ Ry(math.radians(R.uniform(-1.0, 1.0))),
                   cham=0.05, uv_world=True, wob=0.012, seed=R.random() * 90)
        x += ln

def bay():
    pc = piece('Bay')
    R = FK.rng('bay')
    # flagged floor
    xs = [XW, 1.25, 2.1, 2.85, XB]
    ys = [Y0, -1.7, -0.75, 0.2, 1.15, 2.0, Y1]
    for i in range(len(xs) - 1):
        for j in range(len(ys) - 1):
            w = xs[i + 1] - xs[i] - 0.05; d = ys[j + 1] - ys[j] - 0.05
            FK.add_box(pc, 'stone', (w, d, 0.06), T((xs[i] + xs[i + 1]) / 2 + R.uniform(-0.01, 0.01), (ys[j] + ys[j + 1]) / 2, 0.025) @
                       Rz(R.uniform(-0.03, 0.03)), cham=0.012, uv_world=True, wob=0.006, seed=i * 7 + j)
    # stone half walls: +X side and back, a course of dressed blocks over the kit plinth
    for frame, L, tag in ((BK.face_xf((XB, Y0, 0), (1, 0, 0)), L_SIDE, 'side'), (BK.face_xf((XB, Y1, 0), (0, 1, 0)), XB - XW, 'back')):
        bw = piece('bw' + tag)
        BK.plinth_run(bw, 0.0, L, zlib.crc32(tag.encode()) % 31)
        stone_course(bw, 0.0, L, PLINTH, 1.02, zlib.crc32(tag.encode()) % 17)
        FK.add_box(bw, 'stone', (L + 0.05, 0.46, 0.07), T(L / 2, 0.14, 1.06), cham=0.025, uv_world=True, wob=0.006, seed=9.0)   # coping
        if tag == 'back':
            # the frame's local x runs along -X here: make the back half wall face +Y
            pass
        BK.merge(pc, bw, frame)
    # vertical boards over the half walls: +X side and the back, topped to the soffit
    def boards(side):
        n0 = int((L_SIDE if side == 'x' else XB - XW) / 0.26)
        for i in range(n0):
            t = (i + 0.5) / n0
            if side == 'x':
                y = Y0 + 0.14 + t * (L_SIDE - 0.28)
                top = soffit(XB - 0.1) + 0.04
                c = (XB - 0.16, y, (1.06 + top) / 2)
                size = (0.05, 0.25, top - 1.06)
                R2_ = FK.rng('bdx%d' % i)
                FK.add_box(pc, 'oak', size, T(*c) @ Rx(math.radians(R2_.uniform(-0.8, 0.8))), cham=0.01, long_axis=2, wob=0.004, seed=i + 0.3)
            else:
                x = XW + 0.14 + t * (XB - XW - 0.28)
                top = soffit(x) + 0.04
                c = (x, Y1 - 0.16, (1.06 + top) / 2)
                R2_ = FK.rng('bdy%d' % i)
                FK.add_box(pc, 'oak', (0.25, 0.05, top - 1.06), T(*c) @ Ry(math.radians(R2_.uniform(-0.8, 0.8))), cham=0.01, long_axis=2,
                           wob=0.004, seed=i + 0.6)
    boards('x'); boards('y')
    # posts, plates, braces
    for (px, py) in ((XB - 0.12, Y0 + 0.12), (XB - 0.12, 0.1), (XB - 0.12, Y1 - 0.12), (2.0, Y1 - 0.12)):
        FK.member(pc, 'oak', (px, py, 0.0), (px, py, soffit(px) + 0.02), 0.26, 0.26, name='bp%.1f%.1f' % (px, py), up=(0, -1, 0))
    FK.member(pc, 'oak', (XB - 0.12, Y0 + 0.12, soffit(XB) - 0.12), (XB - 0.12, Y1 - 0.12, soffit(XB) - 0.12), 0.22, 0.22, name='bplate_x', up=(-1, 0, 0))
    FK.member(pc, 'oak', (XW + 0.1, Y0 + 0.10, soffit(XW + 0.1) - 0.12), (XB - 0.12, Y0 + 0.10, soffit(XB) - 0.12), 0.24, 0.24, name='bfront', up=(0, -1, 0))
    FK.member(pc, 'oak', (XW + 0.1, Y1 - 0.12, soffit(XW + 0.1) - 0.12), (XB - 0.12, Y1 - 0.12, soffit(XB) - 0.12), 0.22, 0.20, name='bplate_y', up=(0, 1, 0))
    FK.member(pc, 'oak', (XB - 0.12, Y0 + 0.12, 1.55), (XB - 0.9, Y0 + 0.10, soffit(XB - 0.9) - 0.12), 0.14, 0.14, name='bbrace1', up=(0, -1, 0))
    FK.member(pc, 'oak', (XB - 0.12, Y0 + 0.12, 1.5), (XB - 0.12, Y0 + 0.9, soffit(XB) - 0.12), 0.13, 0.13, name='bbrace2', up=(-1, 0, 0))
    # coal bin in the back corner, a heap of coal in it
    bx, by = 2.75, 2.12
    for (cx, cy, sx, sy) in ((bx, by - 0.38, 1.0, 0.05), (bx - 0.5, by, 0.05, 0.76), (bx + 0.5, by, 0.05, 0.76)):
        FK.add_box(pc, 'oak', (sx, sy, 0.62), T(cx, cy, 0.34), cham=0.012, long_axis=2, seed=cx + cy)
    FK.add_box(pc, 'oak', (1.0, 0.7, 0.04), T(bx, by, 0.03), cham=0.0, seed=2.2)
    for k in range(34):
        r = FK.rng('coalbin%d' % k)
        blob(pc, 'coal', (bx + r.uniform(-0.38, 0.38), by + r.uniform(-0.25, 0.28), 0.12 + 0.3 * max(0.0, 1 - ((bx - 0) ** 0 * 0)) * r.uniform(0.2, 1.0)),
             (0.07, 0.065, 0.055), k, sub=0)
    # coal and ash trodden out on the floor near the hearth, a swept ash apron
    blob(pc, 'ash', (1.55, 0.6, 0.06), (0.45, 0.8, 0.012), 1.0, sub=1, flat=0.0)
    for k in range(14):
        r = FK.rng('floorcoal%d' % k)
        blob(pc, 'coal', (r.uniform(1.0, 2.0), r.uniform(-0.9, 1.9), 0.075), (0.045, 0.04, 0.03), k + 40, sub=0)
    # the anvil stump's mark: a charred ring and an ash ring on the flags
    disc(pc, 'soot', (ANVIL_XY[0], ANVIL_XY[1], 0.062), 0.36, 0.012, 20, wob=0.1, seed=1.0)
    disc(pc, 'ash', (ANVIL_XY[0], ANVIL_XY[1], 0.066), 0.24, 0.008, 18, wob=0.15, seed=2.0)
    # bar stock leaning in the bay's front corner
    for k in range(5):
        FK.member(pc, 'iron', (XB - 0.35 - k * 0.03, 2.0 - 0.4 + k * 0.045, 0.05), (XB - 0.5 - k * 0.03, 1.45 + k * 0.04, 1.75), 0.03, 0.03, name='bar%d' % k, up=(0, -1, 0))
    return pc

# ----------------------------------------------------------------------------- hearth and chimney
def hearth():
    pc = piece('Hearth')
    bx0, bx1, by0, by1 = BREAST
    oy0, oy1, oz0, oz1 = OPEN
    xb = 0.82          # back of the recess
    S = lambda size, c, seed, cuts=(0, 0, 0), mat='stone_dk', cham=0.03: FK.add_box(
        pc, mat, size, T(*c), cham=cham, cuts=cuts, uv_world=True, wob=0.012, seed=seed)
    # jambs, hearth base, back fill, block over the mantel
    S((bx1 - bx0, oy0 - by0, 2.15), ((bx0 + bx1) / 2, (by0 + oy0) / 2, 1.075), 1.0, cuts=(1, 0, 3))
    S((bx1 - bx0, by1 - oy1, 2.15), ((bx0 + bx1) / 2, (oy1 + by1) / 2, 1.075), 2.0, cuts=(1, 0, 3))
    S((bx1 - bx0 + 0.4, oy1 - oy0 + 0.0, oz0), ((bx0 + bx1 + 0.4) / 2, (oy0 + oy1) / 2, oz0 / 2), 3.0, cuts=(1, 1, 1))
    S((xb - bx0, oy1 - oy0, oz1 - oz0), ((bx0 + xb) / 2, (oy0 + oy1) / 2, (oz0 + oz1) / 2), 4.0)
    S((bx1 - bx0, by1 - by0, 0.70), ((bx0 + bx1) / 2, (by0 + by1) / 2, 2.5), 5.0, cuts=(1, 1, 1))
    S((bx1 - bx0 - 0.06, by1 - by0 - 0.3, 0.30), ((bx0 + bx1) / 2 - 0.03, (by0 + by1) / 2, 2.98), 6.0, cham=0.04)
    # oak mantel beam across the opening, with two stone corbels
    FK.add_box(pc, 'oak', (0.34, by1 - by0 + 0.30, 0.30), T(bx1 - 0.06, (by0 + by1) / 2, 2.0), cham=0.035, cuts=(0, 3, 0), long_axis=1,
               wob=0.006, seed=7.0)
    for yy in (oy0 - 0.02, oy1 + 0.02):
        S((0.22, 0.20, 0.22), (bx1 - 0.02, yy, 1.78), 8.0 + yy, cham=0.03)
    # soot-black lining: back, jambs and the underside of the mantel
    q = FK.quad_grid
    q(pc, 'soot', [(xb, oy1, oz0), (xb, oy0, oz0), (xb, oy0, oz1), (xb, oy1, oz1)], 3, 3, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    q(pc, 'soot', [(xb, oy0, oz0), (bx1 + 0.02, oy0, oz0), (bx1 + 0.02, oy0, oz1), (xb, oy0, oz1)], 2, 2, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    q(pc, 'soot', [(bx1 + 0.02, oy1, oz0), (xb, oy1, oz0), (xb, oy1, oz1), (bx1 + 0.02, oy1, oz1)], 2, 2, uv_fn=lambda c: (c.x / TILE, c.z / TILE))
    q(pc, 'soot', [(xb, oy0, oz1), (bx1 + 0.02, oy0, oz1), (bx1 + 0.02, oy1, oz1), (xb, oy1, oz1)], 2, 2, uv_fn=lambda c: (c.x / TILE, c.y / TILE))
    # hearth slab: brick floor inside, stone apron outside
    FK.add_box(pc, 'brick', (bx1 - xb + 0.05, oy1 - oy0, 0.045), T((xb + bx1) / 2 + 0.02, (oy0 + oy1) / 2, oz0 + 0.02), cham=0.01, uv_world=True, seed=3.3)
    FK.add_box(pc, 'stone', (0.46, oy1 - oy0 + 0.35, 0.10), T(bx1 + 0.20, (oy0 + oy1) / 2, oz0 - 0.03), cham=0.03, uv_world=True, wob=0.008, seed=3.4)
    # a swept hearth with a cold ash smear, a few spare bricks, a poker's worth of cinder
    blob(pc, 'ash', (1.12, 0.60, oz0 + 0.052), (0.34, 0.42, 0.012), 6.0, sub=1)
    for k, (dx, dy, yaw) in enumerate(((0.95, 0.12, 0.3), (1.30, 1.06, 1.1), (1.24, 1.10, 0.8))):
        FK.add_box(pc, 'brick', (0.22, 0.10, 0.065), T(dx, dy, oz0 + 0.075 + (0.065 if k == 2 else 0)) @ Rz(yaw), cham=0.008, uv_world=True, seed=k + 40)
    return pc

def chimney():
    pc = piece('Chimney')
    x0, x1, y0, y1 = STACK
    top = STACK_TOP
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    base = 2.6
    def bb(size, c, seed, cuts=(0, 0, 0), mat='brick'):
        FK.add_box(pc, mat, size, T(*c), cham=0.03, cuts=cuts, uv_world=True, wob=0.012, seed=seed)
    sx, sy = x1 - x0, y1 - y0
    bb((sx, sy, 5.7 - base), (cx, cy, (base + 5.7) / 2), 1, cuts=(1, 1, 5))
    # dressed stone quoins up the shaft's corners
    z = base + 0.2; i = 0
    R = FK.rng('quoin')
    while z < 5.6:
        h = 0.34 + R.uniform(-0.03, 0.03)
        for (qx, qy, sgx, sgy) in ((x0, y0, -1, -1), (x1, y0, 1, -1), (x0, y1, -1, 1), (x1, y1, 1, 1)):
            ln = R.uniform(0.34, 0.44); sh = 0.18
            lx, ly = (ln, sh) if i % 2 else (sh, ln)
            FK.add_box(pc, 'stone', (lx, ly, h - 0.03), T(qx - sgx * (lx / 2 - 0.05), qy - sgy * (ly / 2 - 0.05), z + h / 2), cham=0.03, uv_world=True,
                       wob=0.008, seed=R.random() * 99)
        z += h; i += 1
    # string course, then the upper shaft, corbelled cap, two pots
    FK.add_box(pc, 'stone', (sx + 0.14, sy + 0.14, 0.16), T(cx, cy, 5.78), cham=0.035, uv_world=True, wob=0.006, seed=12)
    sx2, sy2 = sx - 0.08, sy - 0.08
    bb((sx2, sy2, top - 0.5 - 5.86), (cx, cy, (5.86 + top - 0.5) / 2), 13, cuts=(1, 1, 3))
    for k, (dz, grow) in enumerate(((0.0, 0.10), (0.14, 0.18), (0.28, 0.13))):
        bb((sx2 + grow * 2, sy2 + grow * 2, 0.14), (cx, cy, top - 0.5 + dz + 0.07), 3 + k)
    FK.add_box(pc, 'stone_dk', (sx2 + 0.20, sy2 + 0.20, 0.07), T(cx, cy, top - 0.03), cham=0.02, uv_world=True, seed=7)
    for k, dy in enumerate((-0.2, 0.22)):
        bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=False, segments=10, radius1=0.12, radius2=0.09, depth=0.42 + 0.06 * k)
        bm.transform(T(cx + 0.02, cy + dy, top + 0.2 + 0.03 * k)); bm.normal_update(); FK.two_sided(bm, 0.02)
        uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, k * 0.3)); FK._merge(pc, bm, 'brick')
        bm = bmesh.new(); bmesh.ops.create_circle(bm, cap_ends=True, segments=10, radius=0.085)
        bm.transform(T(cx + 0.02, cy + dy, top + 0.15)); uvl = bm.loops.layers.uv.new('UVMap')
        FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0, 0)); FK._merge(pc, bm, 'soot')
    # the stack leans out a little, about 1.2 deg about its foot
    rot = T(cx, cy, 3.2) @ Ry(math.radians(1.2)) @ T(-cx, -cy, -3.2)
    bmesh.ops.transform(pc.bm, matrix=rot, verts=[v for v in pc.bm.verts if v.co.z > 3.2])
    # lead collar where the shaft leaves the catslide, and an iron stay back to the steep slope
    zc = R2z(cx)
    for (ox, oy, sxx, syy) in ((x1 + 0.06, cy, 0.14, sy + 0.30), (x0 - 0.0, y1 + 0.06, sx + 0.16, 0.14), (x0, y0 - 0.06, sx + 0.16, 0.14)):
        FK.add_box(pc, 'iron', (sxx, syy, 0.07), T(ox if sxx > 0.2 else cx, oy, zc + 0.08), cham=0.015, seed=ox + oy)
    z = 4.7
    xs_ = x0 + math.tan(math.radians(1.2)) * (z - 3.2)
    xr = XR + (RIDGE + 0.2 - 0.10 - z) / SL
    a = Vector((xr, cy, z)); b = Vector((xs_ + 0.05, cy, z))
    FK.add_box(pc, 'iron', ((b - a).length, 0.045, 0.045), T(*((a + b) / 2)), cham=0.0, long_axis=0, seed=z)
    FK.add_box(pc, 'iron', (0.04, sy + 0.1, 0.07), T(xs_ + 0.02, cy, z), cham=0.0, seed=z + 1)
    FK.add_box(pc, 'iron', (0.03, 0.10, 0.22), T(xr + 0.05, cy, z - 0.06) @ Ry(math.atan(SL)), cham=0.0, seed=z + 2)
    return pc

def vane():
    pc = piece('Vane')
    c = Vector((XR, 0.1, RIDGE + 0.12 - 0.09))
    FK.add_box(pc, 'iron', (0.03, 0.03, 0.95), T(c.x, c.y, c.z + 0.47), cham=0.0, seed=1.0)
    for k in range(4):
        a = k * math.pi / 2
        FK.add_box(pc, 'iron', (0.30, 0.02, 0.02), T(c.x, c.y, c.z + 0.30) @ Rz(a) @ T(0.1, 0, 0), cham=0.0, seed=k)
    # a hammer-and-horseshoe vane (flat), facing along Y
    FK.add_box(pc, 'iron', (0.02, 0.62, 0.07), T(c.x, c.y, c.z + 0.78), cham=0.0, seed=2.0)
    FK.add_box(pc, 'iron', (0.02, 0.12, 0.16), T(c.x, c.y - 0.26, c.z + 0.80), cham=0.0, seed=3.0)
    pts = [Vector((c.x, c.y + 0.31 + math.sin(a) * 0.11, c.z + 0.78 + math.cos(a) * 0.11)) for a in [0.6 + i * 5.0 / 7 for i in range(8)]]
    BK.tube(pc, 'iron', pts, 0.016, 4)
    return pc

# ----------------------------------------------------------------------------- yard
def sign_bracket(pc):
    """Scrolled iron arm out of the front-right corner post along +X, with two hooks for the sign's chains."""
    z = 2.60; y = Y0 - 0.14
    FK.add_box(pc, 'iron', (1.30, 0.05, 0.055), T(XW + 0.65, y, z), cham=0.005, long_axis=0, seed=1.0)
    FK.add_box(pc, 'iron', (0.07, 0.14, 0.55), T(XW - 0.05, y + 0.07, z - 0.1), cham=0.005, seed=2.0)
    FK.member(pc, 'iron', (XW - 0.02, y, z - 0.62), (XW + 0.85, y, z - 0.02), 0.04, 0.05, name='sbrace', up=(0, -1, 0))
    sc = [Vector((XW + 0.28 + math.cos(a) * r, y, z - 0.07 - 0.04 - math.sin(a) * r)) for a, r in [(i * 0.55, 0.09 + 0.012 * i) for i in range(11)]]
    BK.tube(pc, 'iron', sc, 0.012, 4)
    for hx in SIGN_HOOKS:
        pts = [Vector((hx + math.sin(a) * 0.04, y, z - 0.04 - 0.04 - (1 - math.cos(a)) * 0.04)) for a in [i * math.pi * 1.3 / 6 for i in range(7)]]
        BK.tube(pc, 'iron', pts, 0.011, 4)
    FK.add_box(pc, 'iron', (0.07, 0.07, 0.07), T(XW + 1.30, y, z), cham=0.01, seed=3.0)

SIGN_HOOKS = (XW + 0.85, XW + 1.35)

def yard():
    pc = piece('Yard')
    R = FK.rng('yard')
    xd = X0 + (DOOR[0] + DOOR[1]) / 2
    for k in range(2):
        FK.add_box(pc, 'stone', (0.85 + R.uniform(-0.06, 0.06), 0.52, 0.07), T(xd + R.uniform(-0.1, 0.1), Y0 - 0.62 - k * 0.58, 0.025) @ Rz(R.uniform(-0.2, 0.2)),
                   cham=0.03, segs=2, uv_world=True, wob=0.012, seed=k + 50)
    # quench trough, front right: oak planks in iron bands on two stone feet, murky water
    tx, ty, tl, tw, th = 2.75, -3.42, 1.55, 0.58, 0.52
    for (c, s) in (((tx, ty - tw / 2 + 0.03, th / 2 + 0.1), (tl, 0.06, th)), ((tx, ty + tw / 2 - 0.03, th / 2 + 0.1), (tl, 0.06, th)),
                   ((tx - tl / 2 + 0.03, ty, th / 2 + 0.1), (0.06, tw - 0.1, th)), ((tx + tl / 2 - 0.03, ty, th / 2 + 0.1), (0.06, tw - 0.1, th)),
                   ((tx, ty, 0.14), (tl - 0.1, tw - 0.1, 0.06))):
        FK.add_box(pc, 'oak', s, T(*c), cham=0.012, long_axis=0 if s[0] > s[1] else 1, wob=0.004, seed=c[0] + c[1])
    for bx_ in (-0.5, 0.0, 0.5):
        for sy_ in (-1, 1):
            FK.add_box(pc, 'iron', (0.05, 0.04, th + 0.02), T(tx + bx_ * tl * 0.9, ty + sy_ * (tw / 2 + 0.005), th / 2 + 0.1), cham=0.0, seed=bx_ + sy_)
    for bx_ in (-0.5, 0.5):
        FK.add_box(pc, 'stone', (0.34, tw + 0.06, 0.12), T(tx + bx_ * (tl - 0.4), ty, 0.06), cham=0.03, uv_world=True, wob=0.01, seed=bx_ + 60)
    FK.quad_grid(pc, 'water', [(tx - tl / 2 + 0.06, ty - tw / 2 + 0.06, th + 0.02), (tx + tl / 2 - 0.06, ty - tw / 2 + 0.06, th + 0.02),
                               (tx + tl / 2 - 0.06, ty + tw / 2 - 0.06, th + 0.02), (tx - tl / 2 + 0.06, ty + tw / 2 - 0.06, th + 0.02)], 1, 1,
                 uv_fn=lambda c: (c.x, c.y))
    # a dipper bucket beside it
    disc(pc, 'oak', (tx - tl / 2 - 0.35, ty + 0.1, 0.17), 0.14, 0.34, 14, wob=0.04, seed=3.0)
    for zz in (0.08, 0.28):
        disc(pc, 'iron', (tx - tl / 2 - 0.35, ty + 0.1, zz), 0.147, 0.025, 14)
    # tool rack on the -X wall, near the front: two posts, a rail, hung tools
    rx = X0 - 0.17
    for ry in (-2.35, -1.05):
        FK.member(pc, 'oak', (rx, ry, 0.0), (rx, ry, 1.75), 0.12, 0.12, name='rkp%.2f' % ry, up=(-1, 0, 0))
    FK.member(pc, 'oak', (rx, -2.35, 1.55), (rx, -1.05, 1.55), 0.10, 0.12, name='rkr', up=(-1, 0, 0))
    FK.add_box(pc, 'oak', (0.42, 1.55, 0.05), T(rx - 0.04, -1.70, 1.80), cham=0.01, long_axis=1, seed=2.0)
    ty0 = X0 - 0.12
    for k, (yy, kind) in enumerate(((-2.2, 'tongs'), (-2.0, 'hammer'), (-1.8, 'tongs'), (-1.6, 'rasp'), (-1.4, 'hammer'), (-1.2, 'shovel'))):
        zt = 1.55
        if kind == 'tongs':
            for s_ in (-1, 1):
                FK.add_box(pc, 'iron', (0.018, 0.018, 0.55), T(ty0 - 0.02, yy + s_ * 0.012, zt - 0.32) @ Rx(s_ * 0.035), cham=0.0, seed=k + s_)
            FK.add_box(pc, 'iron', (0.03, 0.07, 0.07), T(ty0 - 0.02, yy, zt - 0.62), cham=0.005, seed=k + 5)
        elif kind == 'hammer':
            FK.add_box(pc, 'oak_lt', (0.03, 0.03, 0.46), T(ty0 - 0.02, yy, zt - 0.28), cham=0.005, seed=k)
            FK.add_box(pc, 'iron', (0.05, 0.13, 0.06), T(ty0 - 0.02, yy, zt - 0.54), cham=0.008, seed=k + 1)
        elif kind == 'rasp':
            FK.add_box(pc, 'iron', (0.025, 0.05, 0.50), T(ty0 - 0.02, yy, zt - 0.30), cham=0.0, seed=k)
            FK.add_box(pc, 'oak_lt', (0.035, 0.035, 0.12), T(ty0 - 0.02, yy, zt - 0.02), cham=0.005, seed=k + 1)
        else:
            FK.add_box(pc, 'oak_lt', (0.03, 0.03, 0.62), T(ty0 - 0.02, yy, zt - 0.34), cham=0.005, seed=k)
            FK.add_box(pc, 'iron', (0.02, 0.22, 0.26), T(ty0 - 0.02, yy, zt - 0.72), cham=0.005, seed=k + 1)
    # grindstone on a trestle, front centre-left
    gx, gy = -0.70, -3.35
    for sx_ in (-0.32, 0.32):
        FK.member(pc, 'oak', (gx + sx_, gy - 0.25, 0.0), (gx + sx_ * 0.4, gy, 0.82), 0.09, 0.09, name='gs1%.1f' % sx_, up=(0, -1, 0))
        FK.member(pc, 'oak', (gx + sx_, gy + 0.25, 0.0), (gx + sx_ * 0.4, gy, 0.82), 0.09, 0.09, name='gs2%.1f' % sx_, up=(0, -1, 0))
    disc(pc, 'wheel', (gx, gy, 0.88), 0.34, 0.14, 28, rot=Ry(math.pi / 2), wob=0.02, seed=4.0)
    disc(pc, 'iron', (gx, gy, 0.88), 0.03, 0.78, 8, rot=Ry(math.pi / 2))
    FK.member(pc, 'iron', (gx + 0.42, gy, 0.88), (gx + 0.42, gy - 0.18, 0.88), 0.025, 0.025, name='crk1', up=(0, 0, 1))
    FK.member(pc, 'oak_lt', (gx + 0.42, gy - 0.18, 0.88), (gx + 0.42, gy - 0.18, 1.05), 0.03, 0.03, name='crk2', up=(1, 0, 0))
    # scrap and charcoal heap under the back eave
    for k in range(46):
        r = FK.rng('heap%d' % k)
        px = -2.6 + r.uniform(0.0, 2.3); py = Y1 + 0.18 + r.uniform(0.0, 0.40)
        hz = 0.06 + 0.5 * max(0.0, 1 - ((px + 1.5) / 1.3) ** 2) * max(0.0, 1 - ((py - Y1 - 0.2) / 0.45) ** 2) * r.uniform(0.4, 1.0)
        blob(pc, 'coal', (px, py, hz), (0.085, 0.08, 0.06), k, sub=0)
    for k in range(5):   # scrap: an old horseshoe, a bent bar, a wheel rim, a cracked pot
        r = FK.rng('scrap%d' % k)
        px = -2.5 + k * 0.5 + r.uniform(-0.1, 0.1); py = Y1 + 0.28 + r.uniform(0, 0.3)
        if k == 1:
            pts = [Vector((px + math.cos(a) * 0.30, py + math.sin(a) * 0.30, 0.04)) for a in [i * math.pi * 2 / 12 for i in range(13)]]
            BK.tube(pc, 'iron', pts, 0.018, 5)
        elif k == 3:
            disc(pc, 'iron', (px, py, 0.20), 0.14, 0.26, 10, wob=0.08, seed=k)
        else:
            FK.member(pc, 'iron', (px - 0.3, py, 0.06 + 0.2 * k % 3 * 0.1), (px + 0.3, py + 0.1, 0.30), 0.04, 0.04, name='scr%d' % k, up=(0, 0, 1))
    # a rain barrel at the back-left corner of the workshop
    disc(pc, 'oak_lt', (X0 + 0.45, Y1 + 0.40, 0.45), 0.34, 0.90, 16, wob=0.03, seed=7.0)
    for zz in (0.18, 0.45, 0.74):
        disc(pc, 'iron', (X0 + 0.45, Y1 + 0.40, zz), 0.355, 0.04, 16)
    disc(pc, 'water', (X0 + 0.45, Y1 + 0.40, 0.88), 0.30, 0.01, 16)
    return pc

# ----------------------------------------------------------------------------- parts
def part_forge():
    pc = piece('Part_Forge')
    oy0, oy1, oz0, oz1 = OPEN
    zb = oz0 + 0.045
    # fire bed: a heap of coal in a low brick rim, ember glow under the crown
    for k in range(4):
        a = [(0.83, 0.07, 0.42, 0.08), (1.40, 0.07, 0.10, 0.50), (0.83, 1.13, 0.42, 0.08), (1.28, 0.0, 0.0, 0.0)][k]
        if k < 3:
            FK.add_box(pc, 'brick', (a[2] if a[2] else 0.10, a[3] if a[3] else 1.0, 0.09), T(a[0] + (0.21 if k != 1 else 0), a[1] + (0.5 if k == 1 else 0), zb + 0.045),
                       cham=0.01, uv_world=True, seed=k + 70)
    blob(pc, 'coal', (1.10, 0.60, zb + 0.07), (0.42, 0.46, 0.17), 11.0, sub=2, flat=0.2)
    for k in range(26):
        r = FK.rng('fb%d' % k)
        a = r.uniform(0, math.tau); d = r.uniform(0.0, 0.34)
        blob(pc, 'coal', (1.10 + math.cos(a) * d * 0.9, 0.60 + math.sin(a) * d, zb + 0.12 + 0.12 * (1 - d / 0.34) + r.uniform(0, 0.05)),
             (0.06, 0.055, 0.045), k + 3, sub=0)
    for k, (ex, ey, er) in enumerate(((1.04, 0.52, 0.11), (1.16, 0.66, 0.09), (0.98, 0.72, 0.07), (1.20, 0.50, 0.06), (1.08, 0.60, 0.13))):
        blob(pc, 'ember', (ex, ey, zb + 0.20 + 0.05 * (k % 2)), (er, er * 0.9, er * 0.45), k + 20, sub=1)
    # tuyere: iron pipe from the bellows side into the fire
    FK.member(pc, 'iron', (1.18, -0.42, zb + 0.14), (1.10, 0.30, zb + 0.16), 0.055, 0.055, name='tuy', up=(0, 0, 1))
    disc(pc, 'iron', (1.10, 0.32, zb + 0.16), 0.045, 0.03, 8, rot=Rx(math.pi / 2))
    # bellows: oak boards, leather folds, a long lever handle, nozzle into the tuyere
    bx, by, bz = 1.30, -0.95, 0.74
    for dz_, ln in ((0.0, 0.78), (0.52, 0.72)):
        FK.add_box(pc, 'oak', (0.52, ln, 0.045), T(bx, by, bz + dz_) @ Rx(math.radians(-5 if dz_ else 3)), cham=0.015, long_axis=1, wob=0.004, seed=dz_ + 1)
    for k in range(4):
        z = bz + 0.07 + k * 0.11
        w = 0.50 - k * 0.0 if k < 2 else 0.50
        FK.add_box(pc, 'leather', (0.50 - abs(k - 1.5) * 0.025, 0.74 - abs(k - 1.5) * 0.02, 0.095), T(bx, by - 0.01 + 0.015 * k, z), cham=0.03, segs=1, seed=k + 2)
    FK.member(pc, 'oak', (bx, by - 0.35, bz + 0.60), (bx, by - 1.0, bz + 1.0), 0.07, 0.07, name='bel_handle', up=(1, 0, 0))
    FK.add_box(pc, 'iron', (0.08, 0.06, 0.06), T(bx, by - 1.0, bz + 1.0), cham=0.01, seed=5.0)
    for yy in (by - 0.37, by + 0.37):
        FK.add_box(pc, 'iron', (0.56, 0.03, 0.05), T(bx, yy, bz + 0.27), cham=0.0, seed=yy)
    FK.member(pc, 'oak', (bx - 0.22, by + 0.38, bz - 0.03), (bx - 0.22, by + 0.38, 0.0), 0.07, 0.07, name='belleg1', up=(1, 0, 0))
    FK.member(pc, 'oak', (bx + 0.22, by + 0.38, bz - 0.03), (bx + 0.22, by + 0.38, 0.0), 0.07, 0.07, name='belleg2', up=(1, 0, 0))
    FK.member(pc, 'oak', (bx - 0.22, by - 0.35, bz - 0.03), (bx - 0.22, by - 0.35, 0.0), 0.07, 0.07, name='belleg3', up=(1, 0, 0))
    FK.member(pc, 'oak', (bx + 0.22, by - 0.35, bz - 0.03), (bx + 0.22, by - 0.35, 0.0), 0.07, 0.07, name='belleg4', up=(1, 0, 0))
    # hood: a sheet-iron canopy slung from the mantel over the fire, rolled rim, a smoke-black underside
    hx0, hx1 = 1.45, 1.95; hy0, hy1 = -0.30, 1.50; hz0, hz1 = 1.52, 2.00
    a = [(hx1, hy0, hz0), (hx1, hy1, hz0), (hx0, hy1 - 0.1, hz1), (hx0, hy0 + 0.1, hz1)]
    n_out = (1.0, 0.0, 0.6)
    FK.quad_grid(pc, 'iron', [a[0], a[1], a[2], a[3]][::-1], 3, 3, uv_fn=lambda c: (c.y / TILE, c.z / TILE))
    FK.quad_grid(pc, 'soot', [(hx1 - 0.02, hy0 + 0.02, hz0 - 0.01), (hx0, hy0 + 0.12, hz1 - 0.02), (hx0, hy1 - 0.12, hz1 - 0.02), (hx1 - 0.02, hy1 - 0.02, hz0 - 0.01)], 2, 2,
                 uv_fn=lambda c: (c.y / TILE, c.x / TILE))
    for (p0, p1) in ((a[0], a[1]), (a[1], a[2]), (a[3], a[0])):
        FK.member(pc, 'iron', Vector(p0), Vector(p1), 0.05, 0.05, name='hood%.1f' % p0[1], up=(1, 0, 0.5))
    for yy in (0.0, 1.2):
        FK.member(pc, 'iron', (hx1 - 0.02, yy, hz0), (hx0 - 0.02, yy, 2.12), 0.04, 0.04, name='hoodstay%.1f' % yy, up=(1, 0, 0))
    # a poker and tongs hung from the hood rim
    FK.add_box(pc, 'iron', (0.02, 0.02, 0.62), T(hx1 + 0.01, -0.12, hz0 - 0.30), cham=0.0, seed=1.0)
    FK.add_box(pc, 'iron', (0.02, 0.02, 0.58), T(hx1 + 0.01, 1.35, hz0 - 0.30), cham=0.0, seed=2.0)
    for s_ in (-1, 1):
        FK.add_box(pc, 'iron', (0.016, 0.016, 0.50), T(hx1 + 0.01, 1.48 + s_ * 0.01, hz0 - 0.27) @ Rx(s_ * 0.03), cham=0.0, seed=3.0 + s_)
    return pc, None, Vector((1.10, 0.60, oz0 + 0.045))

def part_anvil():
    pc = piece('Part_Anvil')
    ax, ay = ANVIL_XY
    sh = 0.60; r = 0.34
    # stump: lumpy bark cylinder with root flares, a lighter cut top
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = []
    for j in range(5):
        z = sh * j / 4
        flare = 1 + 0.22 * max(0.0, 1 - z / 0.16) ** 2
        rings.append([bm.verts.new((math.cos(a) * r * flare * (1 + 0.07 * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, z * 3)))),
                                    math.sin(a) * r * flare * (1 + 0.07 * noise.noise(Vector((math.cos(a) * 2 + 4, math.sin(a) * 2, z * 3)))), z))
                      for a in [k * math.tau / 18 for k in range(18)]])
    for j in range(4):
        for k in range(18):
            f = bm.faces.new((rings[j][k], rings[j][(k + 1) % 18], rings[j + 1][(k + 1) % 18], rings[j + 1][k]))
            for l in f.loops:
                l[uvl].uv = (math.atan2(l.vert.co.y, l.vert.co.x) * r / TILE * 1.3, l.vert.co.z / TILE)
    bm.transform(T(ax, ay, 0)); bm.normal_update(); FK._merge(pc, bm, 'oak')
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    f = bm.faces.new([bm.verts.new(v.co) for v in reversed(rings[-1])]) if False else None
    top = [(math.cos(a) * r * (1 + 0.07 * noise.noise(Vector((math.cos(a) * 2, math.sin(a) * 2, sh * 3)))),
            math.sin(a) * r * (1 + 0.07 * noise.noise(Vector((math.cos(a) * 2 + 4, math.sin(a) * 2, sh * 3)))), sh) for a in [k * math.tau / 18 for k in range(18)]]
    poly(pc, 'oak_lt', [(ax + x, ay + y, z) for x, y, z in top], Vector((0, 0, 1)), lambda c: ((c.x - ax) / TILE + 0.3, (c.y - ay) / TILE + 0.3))
    # anvil: foot, waist, face plate, heel, horn (a tapered cone), hardy hole, all one dark forged iron
    z0 = sh + 0.0
    ba = lambda size, c, seed, cham=0.012, rot=None: FK.add_box(pc, 'iron', size, T(ax + c[0], ay + c[1], z0 + c[2]) @ (rot if rot else Matrix.Identity(4)), cham=cham, seed=seed)
    ba((0.50, 0.26, 0.06), (0, 0, 0.03), 1.0)
    ba((0.30, 0.17, 0.10), (0, 0, 0.11), 2.0)
    ba((0.22, 0.14, 0.10), (0, 0, 0.21), 3.0, rot=None)
    ba((0.64, 0.20, 0.075), (0, 0, 0.30), 4.0, cham=0.015)
    ba((0.20, 0.17, 0.05), (-0.31, 0, 0.30), 5.0)
    bm = bmesh.new(); bmesh.ops.create_cone(bm, cap_ends=True, segments=10, radius1=0.085, radius2=0.018, depth=0.34)
    bm.transform(T(ax + 0.46, ay, z0 + 0.30) @ Ry(math.pi / 2) @ Matrix.Diagonal((1, 0.9, 1, 1)))
    bm.normal_update(); uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (0.2, 0.2)); FK._merge(pc, 'iron' and bm, 'iron')
    ba((0.05, 0.05, 0.02), (-0.18, 0, 0.343), 6.0, cham=0.0)
    # hammer lying on the stump, handle toward the front, and a short bar
    hc = Vector((ax - 0.04, ay - 0.22, sh + 0.025))
    FK.add_box(pc, 'oak_lt', (0.035, 0.46, 0.035), T(*hc) @ Rz(math.radians(18)), cham=0.006, long_axis=1, seed=1.0)
    FK.add_box(pc, 'iron', (0.12, 0.05, 0.055), T(hc.x + 0.07, hc.y + 0.23, hc.z + 0.01) @ Rz(math.radians(18)), cham=0.008, seed=2.0)
    FK.add_box(pc, 'iron', (0.04, 0.05, 0.05), T(hc.x + 0.13, hc.y + 0.23, hc.z + 0.01) @ Rz(math.radians(18)), cham=0.005, seed=3.0)
    FK.add_box(pc, 'iron', (0.03, 0.34, 0.03), T(ax + 0.20, ay - 0.16, sh + 0.02) @ Rz(math.radians(-40)), cham=0.0, seed=4.0)
    return pc, None, Vector((ax, ay, 0.0))

def part_sign():
    pc = piece('Part_Sign')
    z = 2.60
    y = Y0 - 0.14 - 0.02
    bw, bh = 0.92, 0.62
    zt = z - 0.12
    zc = zt - 0.28 - bh / 2
    for hx in SIGN_HOOKS:
        for k in range(6):
            FK.add_box(pc, 'iron', (0.03, 0.012, 0.055), T(hx, y, zt - 0.03 - k * 0.048) @ Rz((k % 2) * 1.57), cham=0.0, seed=k + hx)
    cx = (SIGN_HOOKS[0] + SIGN_HOOKS[1]) / 2
    FK.add_box(pc, 'door', (bw, 0.07, bh), T(cx, y, zc), cham=0.02, long_axis=0, wob=0.003, seed=1.0)
    for (ox, sz) in ((-bw / 2, (0.04, 0.09, bh + 0.02)), (bw / 2, (0.04, 0.09, bh + 0.02))):
        FK.add_box(pc, 'oak', sz, T(cx + ox, y, zc), cham=0.01, long_axis=2, seed=ox)
    for oz_ in (-bh / 2, bh / 2):
        FK.add_box(pc, 'oak', (bw + 0.04, 0.09, 0.04), T(cx, y, zc + oz_), cham=0.01, long_axis=0, seed=oz_)
    for s_ in (-1, 1):
        yf = y + s_ * 0.045
        # gilt anvil on both faces
        ga = lambda size, c, rot=None: FK.add_box(pc, 'gilt', size, T(cx + c[0], yf, zc + c[1]) @ (rot if rot else Matrix.Identity(4)), cham=0.0, seed=c[0] + c[1] + s_)
        ga((0.34, 0.016, 0.06), (0.0, 0.03))
        ga((0.14, 0.016, 0.10), (-0.02, -0.04))
        ga((0.28, 0.016, 0.04), (0.0, -0.11))
        ga((0.16, 0.016, 0.045), (0.24, 0.045), Ry(math.radians(-8)))
        # iron horseshoe above, open end up
        pts = [Vector((cx - 0.04 + math.sin(a) * 0.12, yf + s_ * 0.008, zc + 0.14 - math.cos(a) * 0.10 + 0.10)) for a in [-2.2 + i * 4.4 / 8 for i in range(9)]]
        BK.tube(pc, 'iron', [Vector((p.x, p.y, p.z)) for p in pts], 0.014, 5)
    # a rim of iron nail heads
    for k in range(10):
        FK.add_box(pc, 'iron', (0.02, 0.015, 0.02), T(cx - bw / 2 + 0.06 + k * (bw - 0.12) / 9, y - 0.04, zc + bh / 2 - 0.04 + (0.0 if k % 2 else -bh + 0.08)), cham=0.0, seed=k)
    return pc, None, Vector((cx, y, z - 0.07))

# ----------------------------------------------------------------------------- whimsy, wear, assembly
LEAN = (0.010, 0.006)

def lean_co(p):
    w = max(0.0, min(1.0, (XW + 1.2 - p.x) / 1.2))       # the open bay stands on its own footing
    return Vector((p.x - LEAN[0] * max(0.0, p.z - PLINTH) * w, p.y - LEAN[1] * max(0.0, p.z - 1.0) * w, p.z))

SOCKETS = {}
HEARTH_C = Vector((1.1, 0.6, 0.9))

def write_wear(ob):
    me = ob.data
    attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER')
    mw = ob.matrix_world
    mats = [m.name for m in me.materials]
    sx, sy = (STACK[0] + STACK[1]) / 2, (STACK[2] + STACK[3]) / 2
    door_w = F_FRONT @ Vector(((DOOR[0] + DOOR[1]) / 2, 0, 0))
    for poly_ in me.polygons:
        n = (mw.to_3x3() @ poly_.normal).normalized()
        mname = mats[poly_.material_index] if mats else ''
        roofish = mname == 'FK_RoofTile'
        for li in poly_.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7)
            streak = noise.noise(Vector((p.x * 4.0, p.y * 4.0, p.z * 0.5)))
            r = max(0.0, 1 - p.z / (0.95 + 0.25 * nz)) ** 1.2 * 0.78
            r = max(r, 0.14 * max(0.0, noise.noise(p * 0.5 + Vector((3.0, 1.0, 2.0)))) + 0.05 * max(0.0, nz))
            if p.x > XW - 0.1 and p.z > 1.0 and abs(p.y - Y1) > 0.01:      # the bay is smoky: a pall under the catslide
                r = max(r, 0.22 + 0.12 * streak)
            if PLATE - 0.6 < p.z < PLATE and (p.x < X0 + 0.5 or p.y > Y1 - 0.4 or p.y < Y0 + 0.4):
                r = max(r, 0.2 + 0.1 * streak)
            # soot: the hearth wall above the opening, the breast, the stack top, the roof round the shaft
            dh = (Vector((p.x, p.y, 0)) - Vector((HEARTH_C.x, HEARTH_C.y, 0))).length
            if p.z < 3.2 and dh < 1.7:
                r = max(r, 0.8 * max(0.0, 1 - dh / 1.7) ** 0.7 * (0.8 + 0.3 * streak) * min(1.0, 0.4 + p.z / 1.5))
            if abs(p.x - sx) < 1.0 and abs(p.y - sy) < 1.0 and p.z > STACK_TOP - 1.5:
                r = max(r, min(0.95, (p.z - (STACK_TOP - 1.5)) / 1.2) * (0.7 + 0.2 * streak))
            if roofish and abs(p.x - sx) < 1.9 and abs(p.y - sy) < 1.9:
                r = max(r, 0.6 * max(0.0, 1 - math.hypot(p.x - sx, p.y - sy) / 1.9))
            if p.x > STACK[0] - 0.1 and STACK[2] - 0.1 < p.y < STACK[3] + 0.1 and 3.0 < p.z < 6.8 and n.x > 0.5:
                r = max(r, 0.35 * (1 - (p.z - 3.0) / 3.8) * (0.7 + 0.4 * streak))
            # roof run-off under windows and the hatch
            for zs_ in (WIN[2] - 0.11, HATCH[2] - 0.04, 1.0):
                dz = zs_ - p.z
                if 0.0 < dz < 0.8 and abs(n.z) < 0.5 and p.x < XW:
                    r = max(r, (1 - dz / 0.8) ** 1.6 * max(0.0, 0.10 + 0.30 * streak))
            g = 0.0
            if roofish and n.z > 0.15:
                z_rel = p.z - (ZE - 0.1)
                g = max(0.0, 1 - z_rel / 1.3) * 0.7 + max(0.0, 0.15 + 0.45 * nz) * 0.3
                if p.x > XW:
                    g = max(g, 0.4 + 0.3 * nz)                      # the low catslide holds the damp
                tv = ((poly_.index * 2654435761) % 1009) / 1009.0
                r = max(r, 0.26 * tv * tv)
            elif n.z > 0.6 and p.z < PLINTH + 0.1:
                g = 0.3 + 0.3 * nz
                if p.y > Y1 - 0.5:
                    g = max(g, 0.55)                                 # the back stays damp
            if p.z < 0.6 and abs(p.y - (-3.42)) < 0.6 and abs(p.x - 2.75) < 1.0:
                g = max(g, 0.4)                                      # trough: algae and splash
                r = max(r, 0.4)
            b = 0.0
            dd = (Vector((p.x, p.y, 0)) - Vector((door_w.x, door_w.y, 0))).length
            if p.z < 0.3 and dd < 1.3 and n.z > 0.5:
                b = 0.9 * (1 - dd / 1.3) ** 0.5
                g = 0.0
            if mname in ('BK_Soot', 'BK_Coal', 'BK_Ash'):
                r = max(r, 0.85 if mname != 'BK_Ash' else 0.1)
            if mname == 'BK_Leather':
                b = max(b, 0.3 * max(0.0, n.z) + 0.2)
            if ob.name == 'Part_Anvil':
                if mname == 'FK_Iron':
                    b = max(b, 0.85 * max(0.0, n.z) ** 2 if p.z > 0.78 else 0.1)
                    r = max(r, 0.25 * (1 - n.z))
                else:
                    r = max(r, 0.3 * max(0.0, 1 - (p.z - 0.4) / 0.2))
                    b = max(b, 0.2 * max(0.0, n.z))
            if ob.name == 'Part_Forge':
                r = max(r, 0.65 if mname != 'BK_Ember' else 0.0)
                if mname == 'FK_Iron':
                    b = max(b, 0.3)
            if ob.name == 'Part_Sign':
                r = max(r, 0.12 * (0.6 + 0.4 * streak))
                if mname == 'BK_Gilt':
                    b = max(b, 0.5)
            attr.data[li].color = (min(1.0, r), min(1.0, g), min(1.0, b), 1.0)

SOFT = {'BK_Sack', 'BK_Leather', 'BK_Coal', 'BK_Ash', 'BK_Ember', 'BK_Water'}

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
    for poly_ in me.polygons:
        if me.materials[poly_.material_index].name in SOFT:
            poly_.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob)
    ob.parent = parent
    if origin is not None:
        ob.location = origin
    bpy.context.view_layer.update()      # write_wear reads matrix_world
    write_wear(ob)
    return ob

def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def build():
    root = bpy.data.objects.new('Smithy', None); bpy.context.scene.collection.objects.link(root)
    BK.ember_mat()
    frame = piece('Frame')
    for pc, xf in ((front_wall(), F_FRONT), (left_wall(), F_LEFT), (back_wall(), F_BACK), (right_wall(), F_RIGHT)):
        BK.merge(frame, pc, xf)
    sb = piece('Bracket'); sign_bracket(sb)
    for pc in (roof(), bay(), hearth(), chimney(), vane(), sb, yard()):
        BK.merge(frame, pc)
    fo = to_object(frame, root)
    total = tris(fo)
    print('  %-12s %6d tris  %d slots' % ('Frame', total, len(fo.data.materials)))
    parts = {}
    for fn, sock in ((part_forge, 'Forge'), (part_anvil, 'Anvil'), (part_sign, 'Sign')):
        pc, xf, s_local = fn()
        s = lean_co(s_local)
        ob = to_object(pc, root, origin=s)
        parts[sock] = ob
        t = tris(ob); total += t
        print('  %-12s %6d tris  %s' % (ob.name, t, ', '.join(m.name for m in ob.data.materials)))
        e = bpy.data.objects.new('SOCKET_' + sock, None); bpy.context.scene.collection.objects.link(e)
        e.parent = fo; e.location = s; e.empty_display_size = 0.3
        SOCKETS[sock] = s
        print('    SOCKET_%s at (%.3f, %.3f, %.3f)' % (sock, s.x, s.y, s.z))
    fx0, fx1 = X0 - 0.10, XB + 0.10
    fy0, fy1 = Y0 - 0.10, Y1 + 0.10
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(T((fx0 + fx1) / 2, (fy0 + fy1) / 2, 0.05) @ Matrix.Diagonal((fx1 - fx0, fy1 - fy0, 0.1, 1)))
    bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root
    fp.display_type = 'WIRE'; fp.hide_render = True
    print('FOOTPRINT x %.2f..%.2f  y %.2f..%.2f  (%.2f x %.2f m; M class max 7.5)' % (fx0, fx1, fy0, fy1, fx1 - fx0, fy1 - fy0))
    allx = [(o.matrix_world @ Vector(c)).x for o in [fo] + list(parts.values()) for c in o.bound_box]
    ally = [(o.matrix_world @ Vector(c)).y for o in [fo] + list(parts.values()) for c in o.bound_box]
    allz = [(o.matrix_world @ Vector(c)).z for o in [fo] + list(parts.values()) for c in o.bound_box]
    print('bounds x %.2f..%.2f  y %.2f..%.2f  z %.2f..%.2f  (foundation +-4.0)' % (min(allx), max(allx), min(ally), max(ally), min(allz), max(allz)))
    print('slots: ' + ', '.join(sorted({m.name for o in [fo] + list(parts.values()) for m in o.data.materials})))
    print('Smithy total tris: %d (cap 35000)' % total)
    return root, fo, parts, fp

# ----------------------------------------------------------------------------- review
VIEWS_DEF = {
    'tq': (Vector((10.5, -13.5, 3.4)), Vector((0.6, 0, 3.6)), 44),
    'tql': (Vector((-11.5, -12.0, 3.2)), Vector((-0.4, 0, 3.6)), 44),
    'back': (Vector((9.5, 13.5, 3.6)), Vector((0.2, 0.5, 3.6)), 44),
    'front': (Vector((0.2, -17.5, 3.2)), Vector((0.2, 0, 3.8)), 42),
    'frame': (Vector((10.5, -13.5, 3.4)), Vector((0.6, 0, 3.6)), 44),
    'close': (Vector((5.4, -7.2, 1.9)), Vector((1.7, 0.0, 1.3)), 42),
    'sign': (Vector((4.0, -7.5, 2.6)), Vector((1.3, -2.8, 2.0)), 40),
    'anvil': (Vector((4.8, -4.6, 1.6)), Vector((2.3, -0.6, 0.8)), 40),
    'framefront': (Vector((0.2, -17.5, 3.2)), Vector((0.2, 0, 3.8)), 42),
}

def review(root, parts):
    sc = bpy.context.scene
    FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(-1.0, -4.0, 0.875))
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
        sc.render.filepath = os.path.join(OUT, 'smithy_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, fo, parts, fp = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects:
            o.select_set(o == root or o.parent in (root, fo))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'Smithy.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y',
                                 apply_scale_options='FBX_SCALE_UNITS', bake_space_transform=True, object_types={'EMPTY', 'MESH'},
                                 mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
        print('exported', os.path.join(OUT, 'Smithy.fbx'))
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'smithy.blend'))
    if '--no-render' not in argv:
        review(root, parts)
