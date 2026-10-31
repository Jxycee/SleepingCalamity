"""KeepHall: Hollowmere's keep hall (fixed landmark on the +4 m terrace beside the bell tower; two states in one FBX).

Root `KeepHall` with children: Shell (ruined start), Frame (rebuilt), Part_Door (pivot SOCKET_Door at the west threshold),
SOCKET_Door under Frame, FOOTPRINT (9 x 6 m body, 0.1 m tall). An old stone church hall: grey rubble walls (same tint as the bell tower)
with pale dressed quoins, round-headed windows (3 on the square side, 2 on the back, two lancets and an oculus in the east gable, a lancet
over the door), two-stage buttresses, a corbelled cornice, a steep slate roof with stone-coped gables, a small bellcote on the east gable
and a stone porch hood over the arched west door. Eave about 5.9 m, ridge about 9.9 m (bellcote 11.2): lower than the 17 m tower.
Shell = ruined state: the west end has fallen (ragged walls, roof gone there, bare rafters, tie beams), windows empty, door gone, bellcote
stub, rubble heaps.

Axes (Blender): z up, metres, origin = footprint centre at ground. Unity (x, y, z) = Blender (-x, z, -y): the WEST door gable is Blender +X,
the square-facing long side (Unity -Z) is Blender +Y (CONTRACT.md says "Blender -Y front"; the mapping, which is binding, puts it at +Y).
Door threshold centre Blender (4.5, -0.5, 0) = Unity local (-4.5, 0, 0.5).

  blender -b --factory-startup --python-exit-code 1 --python Art/Blender/town/sc_keep_hall.py -- --out <scratch>
      [--no-render] [--export] [--tris] [--views sq,tq,back,frame,shell,door] [--rres 1000] [--samples 20]
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector, Matrix, noise
import sc_facade_kit as FK

OUT = FK.OUT; argv = FK.argv; TILE = FK.TILE
RRES = int(FK.arg('--rres', '1000')); SAMPLES = int(FK.arg('--samples', '20'))
VIEWS = FK.arg('--views', 'sq,tq,back,frame,shell,door').split(',')

HX, HY = 4.5, 3.0            # half body
T = 0.7                      # wall thickness
CT = 5.34                    # long-wall top under the cornice
RZ = 9.8; SE = 5.9           # tile surface at the ridge and at the wall line
K = (RZ - SE) / HY           # slope (rise per metre)
OV = 0.45                    # eave overhang
RXH = 4.3                    # roof half length
DOOR_U = 2.5                 # west gable: door centre (y = -0.5)
DHW = 0.65                   # door half width
DZ0, DZS = 0.06, 2.0         # door sill z, arch springing z
WIN = dict(z0=1.7, zs=4.1, hw=0.5)

EXTRA = {
    'rub': lambda: FK.tile_mat('KH_Rubble', 'B1_town/sandstone', tint=(0.62, 0.60, 0.57), normal=1.4),   # same grey as BT_Rubble
    'slate': lambda: FK.tile_mat('KH_Slate', 'B1_town/sandstone', tint=(0.30, 0.36, 0.47), normal=1.6),
    'oakw': lambda: FK.tile_mat('KH_OakWeathered', 'B1_town/oak_timber', tint=(0.62, 0.60, 0.58), normal=1.3),
    'dooroak': lambda: FK.tile_mat('KH_DoorOak', 'B1_town/oak_timber', tint=(1.25, 1.05, 0.85), normal=1.2),
    'lime': lambda: FK.tile_mat('KH_Limewash', 'B1_town/plaster', tint=(0.92, 0.88, 0.80), normal=0.5),
    'flag': lambda: FK.tile_mat('KH_Flag', 'B1_town/sandstone', tint=(0.56, 0.54, 0.51), normal=0.8),
    'bronze': lambda: FK.tile_mat('KH_Bronze', 'B1_town/iron', tint=(2.1, 1.5, 0.75)),
}
_orig_M = FK.M
FK.M = lambda k: EXTRA[k]() if k in EXTRA else _orig_M(k)

def lean_member(pc, mat, a, b, w, d, cham=0.025, name='m', sag=0.0, wob=0.006, extend=0.0, up=None):
    a = Vector(a); b = Vector(b)
    axis = (b - a); L = axis.length + 2 * extend
    if L < 1e-4:
        return
    axis.normalize()
    up = Vector(up) if up else Vector((0, -1, 0))
    side = axis.cross(up)
    if side.length < 1e-4:
        up = Vector((1, 0, 0)); side = axis.cross(up)
    side.normalize(); up = side.cross(axis).normalized()
    R = FK.rng('khmem' + name)
    rot = Matrix.Rotation(math.radians(R.uniform(-1.6, 1.6)), 4, up)
    xf = Matrix.Translation((a + b) / 2) @ rot @ Matrix((side, up, axis)).transposed().to_4x4()
    cuts = max(1, int(L / 1.8)) if sag else 0
    bend = (lambda p, L=L: Vector((sag * (1 - (p.z / L * 2) ** 2), 0, 0))) if sag else None
    FK.add_box(pc, mat, (w, d, L), xf, cham=cham, cuts=(0, 0, cuts), long_axis=2, bend=bend, wob=wob, seed=R.random() * 100)

def piece(n): return FK.Piece(n)

def orient(pc, n0, expect):
    pc.bm.faces.ensure_lookup_table(); pc.bm.normal_update()
    bad = []
    for f in pc.bm.faces[n0:]:
        e = expect(f.calc_center_median()) if callable(expect) else expect
        if f.normal.dot(e) < 0: bad.append(f)
    if bad: bmesh.ops.reverse_faces(pc.bm, faces=bad)

def orient_bm(bm, expect):
    bm.normal_update()
    bad = [f for f in bm.faces if f.normal.dot(expect(f.calc_center_median())) < 0]
    if bad: bmesh.ops.reverse_faces(bm, faces=bad)

def merge(dst, src, xf=None):
    if xf is not None:
        bmesh.ops.transform(src.bm, matrix=xf, verts=src.bm.verts[:])
    if '--tris' in argv and src.bm.verts:
        co = [v.co for v in src.bm.verts]
        print('    %-12s %6d tris  x %.1f..%.1f y %.1f..%.1f z %.1f..%.1f' % ((src.name, sum(len(f.verts) - 2 for f in src.bm.faces)) + tuple(f(c[i] for c in co) for i in range(3) for f in (min, max))))
    remap = []
    for m in src.mats:
        if m not in dst.mats: dst.mats.append(m)
        remap.append(dst.mats.index(m))
    vmap = {v: dst.bm.verts.new(v.co) for v in src.bm.verts}
    for f in src.bm.faces:
        try: nf = dst.bm.faces.new([vmap[v] for v in f.verts])
        except ValueError: continue
        nf.material_index = remap[f.material_index] if remap else 0
        for l, nl in zip(f.loops, nf.loops):
            nl[dst.uv].uv = l[src.uv].uv
    src.bm.free()

def face_xf(origin, n):
    """Wall frame: local x = along the wall (left to right seen from outside), local -y = outward, z up."""
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    return Matrix.Translation(origin) @ Matrix((t, -n, Vector((0, 0, 1)))).transposed().to_4x4()

def flat(pc, mat, pts, facing, nu=1, nv=1):
    a, b, c, d = [Vector(p) for p in pts]
    if (b - a).cross(d - a).dot(Vector(facing)) < 0:
        b, d = d, b
    ax = max(range(3), key=lambda i: abs(facing[i]))
    uv = {0: lambda p: (p.y / TILE, p.z / TILE), 1: lambda p: (p.x / TILE, p.z / TILE), 2: lambda p: (p.x / TILE, p.y / TILE)}[ax]
    FK.quad_grid(pc, mat, [a, b, c, d], nu, nv, uv_fn=uv)

def poly(pc, mat, pts, facing):
    """N-gon whose normal faces `facing`."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    f = bm.faces.new([bm.verts.new(p) for p in pts]); f.normal_update()
    if f.normal.dot(Vector(facing)) < 0: bmesh.ops.reverse_faces(bm, faces=[f])
    ax = max(range(3), key=lambda i: abs(facing[i]))
    bm.faces.ensure_lookup_table()
    for l in bm.faces[0].loops:
        p = l.vert.co
        l[uvl].uv = {0: (p.y, p.z), 1: (p.x, p.z), 2: (p.x, p.y)}[ax]
    bm.normal_update(); FK._merge(pc, bm, mat)

# ----------------------------------------------------------------------------- stone walls (local frame: u along, -y out, z up)
def pillow(pc, u0, u1, z0, z1, R):
    g = 0.018
    us = [u0 + g, (u0 + u1) / 2, u1 - g]; zs = [z0 + g, (z0 + z1) / 2, z1 - g]
    dep = [[0.0, R.uniform(0.02, 0.04), 0.0], [R.uniform(0.02, 0.04), R.uniform(0.05, 0.09), R.uniform(0.02, 0.04)], [0.0, R.uniform(0.02, 0.04), 0.0]]
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    V = [[bm.verts.new((us[i], -dep[j][i], zs[j])) for i in range(3)] for j in range(3)]
    off = (R.random(), R.random())
    for j in range(2):
        for i in range(2):
            f = bm.faces.new((V[j][i], V[j][i + 1], V[j + 1][i + 1], V[j + 1][i]))
            for l in f.loops:
                l[uvl].uv = (l.vert.co.x / TILE + off[0], l.vert.co.z / TILE + off[1])
    bm.normal_update(); FK._merge(pc, bm, 'rub')

def stone_wall(pc, u0, u1, z0, z1, top, holes, seed):
    R = FK.rng('khsw' + seed); z = z0; ci = 0
    while z < z1 - 0.05:
        h = R.uniform(0.30, 0.46)
        if z + h > z1 - 0.12: h = z1 - z
        blocks = sorted((hh[0], hh[1]) for hh in holes if hh[2] < z + h - 0.02 and hh[3] > z + 0.02)
        segs = []; a = u0
        for b0, b1 in blocks:
            if b0 > a: segs.append((a, b0))
            a = max(a, b1)
        if a < u1: segs.append((a, u1))
        for s0, s1 in segs:
            u = s0; first = True
            while u < s1 - 0.04:
                ln = R.uniform(0.2, 0.95) if (first and ci % 2) else R.uniform(0.5, 0.95)
                if s1 - (u + ln) < 0.3: ln = s1 - u
                zt = min(top(u + ln / 2), z1)
                hh = min(z + h, zt) - z
                if hh > 0.12: pillow(pc, u, u + ln, z, z + hh, R)
                u += ln; first = False
        z += h; ci += 1

def backing(pc, u0, u1, z0, z1, top, holes, y, mat, facing, cap):
    cuts = sorted(set([u0, u1] + [h[0] for h in holes if u0 < h[0] < u1] + [h[1] for h in holes if u0 < h[1] < u1]))
    us = []
    for a, b in zip(cuts, cuts[1:]):
        n = max(1, int((b - a) / 0.8 + 0.99)); us += [a + (b - a) * k / n for k in range(n)]
    us.append(cuts[-1])
    for a, b in zip(us, us[1:]):
        mid = (a + b) / 2; zt = min(z1, top(mid))
        segs = [(z0, zt)]
        for h in holes:
            if h[0] < mid < h[1]:
                new = []
                for s0, s1 in segs:
                    if h[3] <= s0 or h[2] >= s1: new.append((s0, s1)); continue
                    if h[2] > s0: new.append((s0, h[2]))
                    if h[3] < s1: new.append((h[3], s1))
                segs = new
        for s0, s1 in segs:
            if s1 - s0 > 0.02:
                flat(pc, mat, [(a, y, s0), (b, y, s0), (b, y, s1), (a, y, s1)], facing)
                if cap and zt < z1 - 0.01 and s1 >= zt - 1e-6:
                    flat(pc, 'stone_dk', [(a, 0.04, zt), (b, 0.04, zt), (b, T, zt), (a, T, zt)], (0, 0, 1))

def arch_holes(o):
    """Stair-stepped holes covering a round-headed opening (rects follow the circle outside)."""
    uc, hw, z0, zs = o['uc'], o['hw'], o['z0'], o['zs']
    hs = [(uc - hw, uc + hw, z0, zs)]
    n = 5
    for i in range(n):
        a0, a1 = math.pi / 2 * i / n, math.pi / 2 * (i + 1) / n
        w = hw * math.cos(a0)
        hs.append((uc - w, uc + w, zs + hw * math.sin(a0) - 0.001, zs + hw * math.sin(a1) + (0.02 if i == n - 1 else 0)))
    return hs

def circ_holes(o):
    uc, zc, r = o['uc'], o['zc'], o['r']
    nb = 8; hs = []
    for j in range(nb):
        zl = zc - r + 2 * r * j / nb; zh = zl + 2 * r / nb
        dmin = 0.0 if zl < zc < zh else min(abs(zl - zc), abs(zh - zc))
        w = math.sqrt(max(r * r - dmin * dmin, 1e-4))
        hs.append((uc - w, uc + w, zl, zh))
    return hs

def box_l(pc, mat, size, c, rot=None, cham=0.03, seed=0.0, segs=1):
    xf = Matrix.Translation(c)
    if rot is not None: xf = xf @ rot
    FK.add_box(pc, mat, size, xf, cham=cham, segs=segs, uv_world=True, wob=0.006, seed=seed)

def surround(pc, o, top, tn, ruin):
    """Dressed stone round the opening: jamb stones, voussoir ring, sill. Local wall frame."""
    R = FK.rng('khsur%.2f%.2f' % (o['uc'], o.get('z0', o.get('zc', 0))))
    def ok(u, z): return z < top(u) - 0.08
    if o['kind'] in ('arch', 'door'):
        uc, hw, z0, zs = o['uc'], o['hw'], o['z0'], o['zs']
        z = z0; i = 0
        while z < zs - 0.05:
            h = min(R.uniform(0.38, 0.48), zs - z + 0.1)
            wj = 0.30 if i % 2 else 0.46
            for sg in (-1, 1):
                u = uc + sg * (hw + wj / 2 - 0.02)
                if ok(u, z + h / 2):
                    box_l(pc, 'stone', (wj, 0.2, h - 0.03), Vector((u, -0.06, z + h / 2)), Matrix.Rotation(math.radians(R.uniform(-1, 1)), 4, 'Y'), seed=R.random() * 90)
            z += h; i += 1
        nv = 9; rin = hw; th = 0.30
        for k in range(nv):
            a = math.pi * (k + 0.5) / nv
            r = rin + th / 2 - 0.02
            p = Vector((uc + math.cos(a) * r, -0.06, zs + math.sin(a) * r))
            if ok(p.x, p.z):
                box_l(pc, 'stone', (th + (0.08 if k % 2 else 0.0), 0.2, math.pi * r / nv * 0.98), p, Matrix.Rotation(-a, 4, 'Y'), seed=k + uc * 7)
        if o['kind'] == 'arch' and ok(uc, o['z0']):
            box_l(pc, 'stone', (2 * hw + 0.66, 0.34, 0.16), Vector((uc, -0.1, z0 - 0.07)), cham=0.03, seed=uc)
        if o['kind'] == 'door':
            box_l(pc, 'stone', (2 * hw + 0.7, tn + 0.9, 0.12), Vector((uc, -0.25, z0 - 0.05)), cham=0.03, seed=3.3)
    else:
        uc, zc, r = o['uc'], o['zc'], o['r']; nv = 12; th = 0.28
        for k in range(nv):
            a = 2 * math.pi * (k + 0.5) / nv
            rr = r + th / 2 - 0.02
            p = Vector((uc + math.cos(a) * rr, -0.06, zc + math.sin(a) * rr))
            if ok(p.x, p.z):
                box_l(pc, 'stone', (th, 0.2, 2 * math.pi * rr / nv * 0.98), p, Matrix.Rotation(-a, 4, 'Y'), seed=k + uc)

def reveals(pc, o, top, tn):
    def ok(u, z): return z < top(u) - 0.05
    if o['kind'] in ('arch', 'door'):
        uc, hw, z0, zs = o['uc'], o['hw'], o['z0'], o['zs']
        for sg, f in ((-1, (1, 0, 0)), (1, (-1, 0, 0))):
            u = uc + sg * hw
            ztop = min(zs, top(u) - 0.05)
            if ztop > z0 + 0.05:
                flat(pc, 'stone', [(u, 0, z0), (u, tn, z0), (u, tn, ztop), (u, 0, ztop)], f)
        na = 10
        for k in range(na):
            a0, a1 = math.pi * k / na, math.pi * (k + 1) / na; am = (a0 + a1) / 2
            p0 = (uc + hw * math.cos(a0), zs + hw * math.sin(a0)); p1 = (uc + hw * math.cos(a1), zs + hw * math.sin(a1))
            if ok((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2):
                flat(pc, 'stone', [(p0[0], 0, p0[1]), (p0[0], tn, p0[1]), (p1[0], tn, p1[1]), (p1[0], 0, p1[1])], (-math.cos(am), 0, -math.sin(am)))
        if ok(uc, z0 + 0.1):
            flat(pc, 'stone', [(uc - hw, 0, z0), (uc + hw, 0, z0), (uc + hw, tn, z0), (uc - hw, tn, z0)], (0, 0, 1))
    else:
        uc, zc, r = o['uc'], o['zc'], o['r']; na = 16
        for k in range(na):
            a0, a1 = 2 * math.pi * k / na, 2 * math.pi * (k + 1) / na; am = (a0 + a1) / 2
            p0 = (uc + r * math.cos(a0), zc + r * math.sin(a0)); p1 = (uc + r * math.cos(a1), zc + r * math.sin(a1))
            if ok((p0[0] + p1[0]) / 2, (p0[1] + p1[1]) / 2):
                flat(pc, 'stone', [(p0[0], 0, p0[1]), (p0[0], tn, p0[1]), (p1[0], tn, p1[1]), (p1[0], 0, p1[1])], (-math.cos(am), 0, -math.sin(am)))

def glazing(pc, o, tn):
    """Glass set back in the reveal with a came lattice."""
    gy = tn * 0.55
    if o['kind'] == 'arch':
        uc, hw, z0, zs = o['uc'], o['hw'], o['z0'], o['zs']
        pts = [(uc - hw, gy, z0), (uc + hw, gy, z0)] + [(uc + hw * math.cos(math.pi * k / 12), gy, zs + hw * math.sin(math.pi * k / 12)) for k in range(0, 13)]
        poly(pc, 'glass', pts, (0, -1, 0))
        n = 3 if hw > 0.4 else 1
        for i in range(1, n + 1):
            u = uc - hw + 2 * hw * i / (n + 1)
            zt = zs + math.sqrt(max(hw * hw - (u - uc) ** 2, 0.0))
            FK.add_box(pc, 'iron', (0.026, 0.022, zt - z0), Matrix.Translation((u, gy - 0.012, (zt + z0) / 2)), cham=0.0, seed=u)
        nh = int((zs - z0) / 0.5)
        for j in range(1, nh + 1):
            FK.add_box(pc, 'iron', (2 * hw, 0.022, 0.026), Matrix.Translation((uc, gy - 0.012, z0 + (zs - z0) * j / (nh + 0.6))), cham=0.0, seed=j)
        FK.add_box(pc, 'iron', (2 * hw, 0.022, 0.03), Matrix.Translation((uc, gy - 0.012, zs)), cham=0.0, seed=9)
        for k in (4, 8):   # arch ring
            a0, a1 = math.pi * (k - 4) / 12, math.pi * k / 12
            FK.add_box(pc, 'iron', (0.02, 0.022, 0.02), Matrix.Translation((uc, gy - 0.012, zs + hw * 0.5)), cham=0.0, seed=k)
    elif o['kind'] == 'circ':
        uc, zc, r = o['uc'], o['zc'], o['r']
        poly(pc, 'glass', [(uc + r * math.cos(2 * math.pi * k / 16), gy, zc + r * math.sin(2 * math.pi * k / 16)) for k in range(16)], (0, -1, 0))
        FK.add_box(pc, 'iron', (2 * r, 0.022, 0.03), Matrix.Translation((uc, gy - 0.012, zc)), cham=0.0, seed=1)
        FK.add_box(pc, 'iron', (0.03, 0.022, 2 * r), Matrix.Translation((uc, gy - 0.012, zc)), cham=0.0, seed=2)
        for sg in (1, -1):
            FK.add_box(pc, 'iron', (2 * r, 0.022, 0.02), Matrix.Translation((uc, gy - 0.012, zc)) @ Matrix.Rotation(sg * math.pi / 4, 4, 'Y'), cham=0.0, seed=sg)

def wall(tag, F, L, top, opens, ruin, tn=T, z1=CT + 0.1):
    pc = piece(tag); holes = []
    for o in opens: holes += (arch_holes(o) if o['kind'] in ('arch', 'door') else circ_holes(o))
    stone_wall(pc, 0, L, -0.1, z1, top, holes, tag)
    backing(pc, 0, L, -0.1, z1, top, holes, 0.04, 'mortar', (0, -1, 0), True)
    backing(pc, 0, L, 0.0, z1, top, holes, tn, 'lime', (0, 1, 0), False)
    for o in opens:
        reveals(pc, o, top, tn); surround(pc, o, top, tn, ruin)
        if not ruin and o['kind'] != 'door': glazing(pc, o, tn)
    merge_pc = (pc, F)
    return merge_pc

# ----------------------------------------------------------------------------- the four walls
W_S = dict(F=face_xf((HX, HY, 0), (0, 1, 0)), L=9.0)       # square side, Blender +Y: u = 4.5 - x
W_W = dict(F=face_xf((HX, -HY, 0), (1, 0, 0)), L=6.0)      # west door gable, Blender +X: u = y + 3
W_N = dict(F=face_xf((-HX, -HY, 0), (0, -1, 0)), L=9.0)    # back, Blender -Y: u = x + 4.5
W_E = dict(F=face_xf((-HX, HY, 0), (-1, 0, 0)), L=6.0)    # east gable, Blender -X: u = 3 - y

def rag_fn(seed, a, b, L, amp=1.1):
    def f(u):
        k = max(0.0, min(1.0, u / L)); k = k * k * (3 - 2 * k)
        return a + (b - a) * k + amp * noise.noise(Vector((u * 1.1, seed * 3.7, 0.3))) + 0.35 * noise.noise(Vector((u * 4.0, seed, 1.0)))
    return f

def gable_prof(u): return RZ - K * abs(u - HY) - 0.05

def tops(ruin):
    if not ruin:
        return dict(S=lambda u: CT, N=lambda u: CT, W=gable_prof, E=gable_prof)
    rs = rag_fn(1, 4.2, 6.0, 9.0); rn = rag_fn(2, CT + 0.5, 3.2, 9.0, 1.3); rw = rag_fn(3, 3.2, 4.2, 6.0, 1.0)
    return dict(S=lambda u: min(CT, rs(u)), N=lambda u: min(CT, rn(u)), W=lambda u: min(gable_prof(u), rw(u)), E=gable_prof)

def opens_for(ruin):
    wins = lambda uc, z0=WIN['z0'], zs=WIN['zs'], hw=WIN['hw']: dict(kind='arch', uc=uc, z0=z0, zs=zs, hw=hw)
    return dict(
        S=[wins(4.5 - x) for x in (-2.9, 0.0, 2.9)],
        N=[wins(4.5 + x) for x in (-1.8, 1.8)],
        W=[dict(kind='door', uc=DOOR_U, hw=DHW, z0=DZ0, zs=DZS), wins(3.0, 5.7, 6.9, 0.28)],
        E=[wins(3.0 - 1.1, 1.8, 4.1, 0.3), wins(3.0 + 1.1, 1.8, 4.1, 0.3), dict(kind='circ', uc=3.0, zc=7.55, r=0.55)])

# ----------------------------------------------------------------------------- outer dressings (world frame)
def plinth(pc, ruin):
    for k, (c, sz) in enumerate((((0, HY + 0.06, 0.26), (2 * HX + 0.3, 0.24, 0.52)), ((0, -HY - 0.06, 0.26), (2 * HX + 0.3, 0.24, 0.52)),
                                 ((HX + 0.06, 0, 0.26), (0.24, 2 * HY + 0.3, 0.52)), ((-HX - 0.06, 0, 0.26), (0.24, 2 * HY + 0.3, 0.52)))):
        FK.add_box(pc, 'stone', sz, Matrix.Translation(c), cham=0.06, segs=1, uv_world=True, wob=0.008, seed=k + 1.0)

def quoins(pc, ruin, tp):
    R = FK.rng('khquoins')
    lim = {(1, 1): 4.2, (1, -1): 3.2, (-1, 1): CT + 0.3, (-1, -1): CT + 0.3} if ruin else {k: CT + 0.3 for k in ((1, 1), (1, -1), (-1, 1), (-1, -1))}
    for (sx, sy), zl in lim.items():
        z = 0.58; i = 0
        while z < zl - 0.25:
            h = 0.40 + R.uniform(-0.03, 0.04)
            if ruin and R.random() < 0.12 and z > 2.0:
                z += h; i += 1; continue
            ln, sh = R.uniform(0.62, 0.78), R.uniform(0.34, 0.42)
            lx, ly = (ln, sh) if (i + (sx * sy > 0)) % 2 else (sh, ln)
            c = Vector((sx * (HX + 0.04 - lx / 2), sy * (HY + 0.04 - ly / 2), z + h / 2))
            FK.add_box(pc, 'stone', (lx, ly, h - 0.03), Matrix.Translation(c) @ Matrix.Rotation(math.radians(R.uniform(-1.5, 1.5)), 4, 'Z'),
                       cham=0.045, segs=1, uv_world=True, wob=0.008, seed=R.random() * 99)
            z += h; i += 1

def buttress(pc, x, y, n, h, w=0.72, d=0.66, seed=0.0):
    n = Vector(n); t = Vector((0, 0, 1)).cross(n)
    H = h + 0.1
    def bend(p):
        tt = (p.z + H / 2) / H
        return Vector((-p.x * 0.28 * tt, -((p.y + d / 2)) * 0.7 * tt ** 1.7, 0))
    c = Vector((x, y, 0)) + n * (d / 2 - 0.08) + Vector((0, 0, H / 2 - 0.1))
    xf = Matrix.Translation(c) @ Matrix((-t, n, Vector((0, 0, 1)))).transposed().to_4x4()
    FK.add_box(pc, 'stone', (w, d, H), xf, cham=0.05, cuts=(0, 0, 3), bend=bend, uv_world=True, wob=0.01, seed=seed)
    FK.add_box(pc, 'stone', (w * 0.8, d * 0.5, 0.22), Matrix.Translation(c + n * (d * 0.08) + Vector((0, 0, H / 2 - 0.05))) @ Matrix((-t, n, Vector((0, 0, 1)))).transposed().to_4x4(),
               cham=0.04, segs=1, uv_world=True, seed=seed + 3)

def buttresses(pc, ruin, tp):
    spec = [(x, HY, (0, 1, 0), 4.4 if abs(x) > 4 else 4.0, 'S', 4.5 - x) for x in (4.15, 1.45, -1.45, -4.15)]
    spec += [(x, -HY, (0, -1, 0), 4.4 if abs(x) > 4 else 4.0, 'N', 4.5 + x) for x in (4.15, 0.0, -4.15)]
    for i, (x, y, n, h, w, u) in enumerate(spec):
        hh = min(h, tp[w](u) - 0.5)
        if ruin and hh < 1.4: continue
        buttress(pc, x, y, n, hh, seed=i * 3.1)

def cornice(pc, ruin, tp):
    R = FK.rng('khcornice')
    for sgn, key, ufn in ((1, 'S', lambda x: 4.5 - x), (-1, 'N', lambda x: 4.5 + x)):
        x = -4.5
        while x < 4.5 - 0.05:
            ln = min(1.5, 4.5 - x)
            if tp[key](ufn(x + ln / 2)) >= CT - 0.05:
                FK.add_box(pc, 'stone', (ln - 0.02, 0.95, 0.3), Matrix.Translation((x + ln / 2, sgn * (HY - 0.35 + 0.0 + 0.0 + 0.0 + 0.0) + sgn * 0.0, CT + 0.15)) @ Matrix.Translation((0, sgn * 0.0, 0)),
                           cham=0.04, segs=1, uv_world=True, wob=0.006, seed=R.random() * 50)
                # shift outward so the cornice overhangs the pillows by 0.15
            x += ln
        xc = -4.35
        while xc < 4.4:
            if tp[key](ufn(xc)) >= CT - 0.05:
                FK.add_box(pc, 'stone', (0.3, 0.3, 0.26), Matrix.Translation((xc, sgn * (HY + 0.07), CT - 0.12)), cham=0.04, uv_world=True, seed=xc * 5)
            xc += 0.75

def slab(pc, mat, c, xdir, ydir, size, cham=0.04, seed=0.0):
    xd = Vector(xdir).normalized(); yd = Vector(ydir).normalized(); zd = xd.cross(yd)
    FK.add_box(pc, mat, size, Matrix.Translation(c) @ Matrix((xd, yd, zd)).transposed().to_4x4(), cham=cham, segs=1, uv_world=True, wob=0.005, seed=seed)

def copings(pc, ruin):
    for sx in ((-1,) if ruin else (-1, 1)):
        for sy in (1, -1):
            n = 4
            for i in range(n):
                y0, y1 = sy * HY * (1 - i / n), sy * HY * (1 - (i + 1) / n)
                z0, z1 = RZ - K * abs(y0) + 0.1, RZ - K * abs(y1) + 0.1
                c = Vector((sx * (HX - 0.35), (y0 + y1) / 2, (z0 + z1) / 2))
                slab(pc, 'stone', c, (1, 0, 0), (0, y1 - y0, z1 - z0), (0.95, math.hypot(y1 - y0, z1 - z0) + (0.08 if i < n - 1 else 0.0), 0.38), seed=sx * 7 + sy * 3 + i)
            # kneeler at the foot
            FK.add_box(pc, 'stone', (0.95, 0.5, 0.5), Matrix.Translation((sx * (HX - 0.35), sy * (HY - 0.12), SE - 0.08)), cham=0.05, segs=1, uv_world=True, seed=sx + sy)

# ----------------------------------------------------------------------------- roof
def slope_pt(side, x, t):
    ye = side * (HY + OV)
    sag = 0.16 * t * (1 - (x / RXH) ** 2)
    kick = 0.2 * max(0.0, 1 - t / 0.18) ** 2
    y = ye * (1 - t)
    return Vector((x, y, RZ - K * abs(y) - sag + kick))

def roof_range(side, t, ruin):
    xa, xb = -RXH, RXH
    if ruin:
        base = -0.4 if side > 0 else -2.2
        xb = base + 1.0 * noise.noise(Vector((t * 3.0, side * 2.0, 0.7))) + 0.5 * noise.noise(Vector((t * 9.0, side, 2.0)))
    return xa, xb

def slates(pc, edge, n, nc, tag, drop=None, width=0.36):
    for c in range(nc):
        R = FK.rng('khcourse%s%d' % (tag, c))
        t0, t1 = c / nc, min(1.0, (c + 1) / nc + 0.03)
        a0, b0 = edge(t0); a1, b1 = edge(t1)
        L = (b0 - a0).length; nt = max(2, int(L / width))
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
        for i in range(nt):
            if drop and drop(c, i, nt): continue
            u0, u1 = i / nt, (i + 1) / nt
            j = R.uniform(-0.012, 0.012) if c else R.uniform(-0.008, 0.04)
            pts = []
            for u in (u0 + 0.002, u1 - 0.002):
                pb = a0.lerp(b0, u) + n * (0.04 + j); pt = a1.lerp(b1, u)
                pts.append((bm.verts.new(pb - n * 0.045), bm.verts.new(pb), bm.verts.new(pt)))
            a, b = pts
            for qi, q in enumerate(((a[1], b[1], b[2], a[2]), (a[0], b[0], b[1], a[1]))):
                f = bm.faces.new(q); f.normal_update()
                want = n if qi == 0 else (a[1].co - a[2].co)
                if f.normal.dot(want) < 0: f.normal_flip()
                for l in f.loops:
                    co = l.vert.co
                    l[uvl].uv = ((co - a0).dot((b0 - a0).normalized()) / TILE, (c * 0.37 + (co - a0).dot((a1 - a0).normalized())) / TILE)
        bm.normal_update(); FK._merge(pc, bm, 'slate')

def roof(ruin):
    pc = piece('Roof'); TH = 0.2
    for side in (1, -1):
        n = Vector((0, side * K, 1)).normalized()
        def deck(p, u, v, side=side):
            xa, xb = roof_range(side, v, ruin)
            return slope_pt(side, xa + (xb - xa) * u, v) - n * 0.06
        def under(p, u, v, side=side):
            xa, xb = roof_range(side, v, ruin)
            return slope_pt(side, xa + (xb - xa) * u, v) - n * TH
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'oakw' if ruin else 'slate', [(0, 0, 0)] * 4, 16, 8, push=deck, uv_fn=lambda c: (c.x / TILE, c.y / TILE)); orient(pc, n0, n)
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'oak', [(0, 0, 0)] * 4, 16, 4, push=under, uv_fn=lambda c: (c.x / TILE, c.y / TILE)); orient(pc, n0, -n)
        drop = (lambda c, i, nt, side=side: FK.rng('khd%d%d%d' % (side, c, i)).random() < (0.12 + 0.12 * c / 22)) if ruin else None
        slates(pc, lambda t, side=side: tuple(slope_pt(side, x, t) for x in roof_range(side, t, ruin)), n, 22, 'kh%d' % side, drop=drop)
        xa, xb = roof_range(side, 0, ruin)
        lean_member(pc, 'oak', slope_pt(side, xa, 0) - n * 0.13, slope_pt(side, xb, 0) - n * 0.13, 0.2, 0.07, name='fascia%d' % side, up=(0, side, 0.4))
        if ruin:   # bare rafters where the covering has gone, only above standing wall
            tpr = tops(True)
            for k in range(11):
                x = -4.0 + k * 0.8
                tt = FK.rng('khraft%d%d' % (side, k)).uniform(0.55, 1.0)
                xb = roof_range(side, 0.5, True)[1]
                wt = tpr['S'](4.5 - x) if side > 0 else tpr['N'](4.5 + x)
                if x < xb - 0.3 or x > xb + 1.2 or wt < CT - 0.25: continue
                lean_member(pc, 'oakw', slope_pt(side, x, 0.0) - n * 0.2, slope_pt(side, x, tt) - n * 0.2, 0.13, 0.17, name='raft%d%d' % (side, k), up=tuple(-n))
    # ridge tiles (ruin: only over the standing east end)
    xa = -RXH; xb = RXH if not ruin else -1.4
    nr = max(2, int((xb - xa) / 0.42))
    for i in range(nr):
        R = FK.rng('khrt%d' % i)
        x = xa + (xb - xa) * (i + 0.5) / nr
        zc = RZ - 0.16 * (1 - (x / RXH) ** 2) - 0.02
        bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); segs = 8; Lt = (xb - xa) / nr + 0.05; rows = []
        for e in (-1, 1):
            rr = 0.17 * (1.0 if e < 0 else 1.06)
            rows.append([bm.verts.new((x + e * Lt / 2 + R.uniform(-0.01, 0.01), -rr * math.cos(math.pi * k / segs), zc + rr * math.sin(math.pi * k / segs) * 0.85 - 0.05)) for k in range(segs + 1)])
        for k in range(segs):
            f = bm.faces.new((rows[0][k], rows[1][k], rows[1][k + 1], rows[0][k + 1]))
            for l in f.loops: l[uvl].uv = (l.vert.co.x / TILE, k * 0.07 / TILE)
        orient_bm(bm, lambda c, zc=zc: Vector((0, c.y, c.z - (zc - 0.05))))
        FK.two_sided(bm); FK._merge(pc, bm, 'slate')
    if ruin:   # tie beams and a broken king post across the open end
        for k, x in enumerate((-1.6, -0.5)):
            lean_member(pc, 'oakw', (x, -HY + 0.2, CT + 0.05), (x, HY - 0.2, CT + 0.05), 0.24, 0.28, name='tie%d' % k, sag=0.04, up=(0, 0, 1))
            if k < 2:
                lean_member(pc, 'oakw', (x, 0, CT + 0.17), (x + 0.1, 0.0, CT + 1.5 + k), 0.2, 0.2, name='king%d' % k, up=(1, 0, 0))
    return pc

# ----------------------------------------------------------------------------- bellcote
def bellcote(pc, ruin):
    xc = -HX + 0.35; zb = RZ + 0.25
    FK.add_box(pc, 'stone', (0.72, 1.3, 0.5), Matrix.Translation((xc, 0, zb + 0.15)), cham=0.05, segs=1, uv_world=True, seed=21)
    for sg in ((1,) if ruin else (1, -1)):
        h = 0.45 if ruin else 0.8
        FK.add_box(pc, 'stone', (0.62, 0.34, h), Matrix.Translation((xc, sg * 0.46, zb + 0.4 + h / 2)), cham=0.045, segs=1, uv_world=True, seed=22 + sg)
    if ruin: return
    zt = zb + 1.2
    for sy in (1, -1):
        c = Vector((xc, sy * 0.38, zt + 0.22)); yd = Vector((0, -sy * 0.76, 0.6))
        slab(pc, 'stone', c + Vector((0, 0, 0.1)), (1, 0, 0), (0, -sy * 0.8, 0.62), (0.82, 0.9, 0.12), seed=30 + sy)
    FK.add_box(pc, 'stone', (0.82, 0.14, 0.14), Matrix.Translation((xc, 0, zt + 0.62)), cham=0.03, uv_world=True, seed=33)
    lean_member(pc, 'oak', (xc, -0.4, zb + 1.5), (xc, 0.4, zb + 1.5), 0.1, 0.1, name='bcbeam', up=(0, 0, 1))
    bell_prof = [(0.04, 0.0), (0.12, 0.01), (0.17, 0.05), (0.19, 0.13), (0.22, 0.26), (0.28, 0.4), (0.33, 0.48), (0.30, 0.5), (0.22, 0.4), (0.15, 0.2), (0.12, 0.06), (0.04, 0.04)]
    seg = 12; top = zb + 1.45
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    rings = [[bm.verts.new(Vector((xc + r * math.cos(2 * math.pi * s / seg), r * math.sin(2 * math.pi * s / seg), top - zz))) for s in range(seg)] for (r, zz) in bell_prof]
    for j in range(len(rings) - 1):
        for s in range(seg):
            f = bm.faces.new((rings[j][s], rings[j + 1][s], rings[j + 1][(s + 1) % seg], rings[j][(s + 1) % seg]))
            for l in f.loops: l[uvl].uv = (math.atan2(l.vert.co.y, l.vert.co.x - xc) * 0.5 / TILE, l.vert.co.z / TILE)
    cap = bm.faces.new(rings[0])
    for l in cap.loops: l[uvl].uv = (l.vert.co.x, l.vert.co.y)
    bm.normal_update(); FK._merge(pc, bm, 'bronze')

# ----------------------------------------------------------------------------- porch hood over the west door
PY = -0.5            # door centre y
PW = 1.1             # porch half width (to the outer face of its side walls)
PX = HX + 0.8        # outer face
def porch_top(u): return 3.05 + K * (PW - abs(u - PW)) - 0.05

def porch(ruin):
    """Returns [(piece, frame)] for the porch front wall and two side walls, plus a pc of roof/floor in world coords."""
    out = []
    y0 = PY - PW
    Ff = face_xf((PX, y0, 0), (1, 0, 0))
    topf = porch_top
    if ruin:
        r = rag_fn(5, 2.2, 3.4, 2.2, 0.8)
        topf = lambda u: min(porch_top(u), r(u))
    o = [dict(kind='arch', uc=PW, hw=0.55, z0=0.1, zs=1.95)]
    out.append(wall('PorchF', Ff, 2 * PW, topf, o, ruin, tn=0.4, z1=5.0))
    sidetop = (lambda u: 2.95) if not ruin else (lambda u: 2.0 + 0.9 * noise.noise(Vector((u * 2.0, 4.0, 0.0))))
    out.append(wall('PorchL', face_xf((HX, y0, 0), (0, -1, 0)), 0.8, sidetop, [], ruin, tn=0.4, z1=3.1))
    out.append(wall('PorchR', face_xf((PX, y0 + 2 * PW, 0), (0, 1, 0)), 0.8, sidetop, [], ruin, tn=0.4, z1=3.1))
    return out

def porch_world(pc, ruin):
    # floor flags and step
    flat(pc, 'flag', [(HX - 0.05, PY - PW + 0.4, 0.05), (PX - 0.4, PY - PW + 0.4, 0.05), (PX - 0.4, PY + PW - 0.4, 0.05), (HX - 0.05, PY + PW - 0.4, 0.05)], (0, 0, 1), 2, 3)
    FK.add_box(pc, 'stone', (0.5, 1.5, 0.14), Matrix.Translation((PX + 0.2, PY, 0.04)), cham=0.04, segs=1, uv_world=True, seed=71)
    if ruin: return
    # slate roof: two slopes, ridge along x
    def edge(side):
        def e(t):
            y = PY + side * (PW + 0.2) * (1 - t); z = 3.05 + K * PW - K * abs(y - PY) - 0.0
            return (Vector((HX - 0.1, y, z)), Vector((PX + 0.12, y, z)))
        return e
    for side in (1, -1):
        n = Vector((0, side * K, 1)).normalized()
        a, b = edge(side)(0), edge(side)(1)
        def deck(p, u, v, side=side, n=n):
            q0, q1 = edge(side)(v)
            return q0.lerp(q1, u) - n * 0.06
        n0 = len(pc.bm.faces); FK.quad_grid(pc, 'slate', [(0, 0, 0)] * 4, 4, 4, push=deck, uv_fn=lambda c: (c.x / TILE, c.y / TILE)); orient(pc, n0, n)
        slates(pc, edge(side), n, 7, 'porch%d' % side)
        lean_member(pc, 'oak', edge(side)(0)[0] - n * 0.12, edge(side)(0)[1] - n * 0.12, 0.2, 0.07, name='pfascia%d' % side, up=(0, side, 0.4))
    for i in range(3):   # ridge cap
        FK.add_box(pc, 'slate', (0.28, 0.3, 0.14), Matrix.Translation((HX + 0.15 + i * 0.3, PY, 3.05 + K * PW + 0.02)), cham=0.03, uv_world=True, seed=i + 60)

# ----------------------------------------------------------------------------- door
def door_part():
    pc = piece('Part_Door'); n = 5; R = FK.rng('khdoor'); pw = 2 * DHW / n
    for i in range(n):
        uc = DOOR_U - DHW + pw * (i + 0.5); du = abs(uc - DOOR_U) + pw / 2 - 0.03
        top = DZS + math.sqrt(max(DHW * DHW - du * du, 0.0)) - 0.03
        FK.add_box(pc, 'dooroak', (pw - 0.01, 0.07, top - DZ0 - 0.02), Matrix.Translation((uc, 0.32, DZ0 + 0.01 + (top - DZ0) / 2)), cham=0.012, cuts=(0, 0, 3), long_axis=2, wob=0.004, seed=R.random() * 80)
    for zz in (DZ0 + 0.4, DZS - 0.05):
        FK.add_box(pc, 'iron', (2 * DHW - 0.1, 0.02, 0.11), Matrix.Translation((DOOR_U, 0.275, zz)), cham=0.0, long_axis=0, seed=zz)
        FK.add_box(pc, 'iron', (0.34, 0.025, 0.15), Matrix.Translation((DOOR_U - DHW + 0.2, 0.272, zz)), cham=0.004, seed=zz + 1)
    FK.add_box(pc, 'iron', (0.07, 0.04, 0.2), Matrix.Translation((DOOR_U + DHW - 0.22, 0.27, 1.1)), cham=0.008, seed=2)
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); r = 0.075; seg = 10
    ring = [bm.verts.new((DOOR_U + DHW - 0.22 + math.cos(2 * math.pi * s / seg) * r, 0.25, 1.1 - 0.17 + math.sin(2 * math.pi * s / seg) * r)) for s in range(seg)]
    bm.faces.new(ring); bm.normal_update(); bm.faces.ensure_lookup_table()
    for l in bm.faces[0].loops: l[uvl].uv = (l.vert.co.x, l.vert.co.z)
    FK._merge(pc, bm, 'iron')
    return pc

# ----------------------------------------------------------------------------- rubble (Shell)
def heaps():
    pc = piece('Heaps'); R = FK.rng('khheaps')
    spots = [(2.2, 1.0, 1.2, 0.7), (3.4, -1.6, 1.0, 0.6), (0.4, -0.9, 0.8, 0.4), (5.9, 1.8, 1.1, 0.7), (3.6, -4.0, 1.3, 0.8), (-0.8, -3.9, 0.8, 0.4),
             (5.7, -2.4, 0.9, 0.55), (1.3, 3.8, 0.9, 0.5), (-1.7, 0.9, 0.7, 0.3)]
    for k, (cx, cy, r, h) in enumerate(spots):
        bm = bmesh.new(); bmesh.ops.create_icosphere(bm, subdivisions=1, radius=1.0)
        for v in bm.verts:
            q = v.co * 1.3 + Vector((k * 3.1, 0, 0))
            d = 1 + 0.28 * noise.noise(q) + 0.1 * noise.noise(q * 3.1)
            v.co = Vector((v.co.x * r * d, v.co.y * r * 0.9 * d, max(v.co.z, -0.1) * h * d))
        bm.transform(Matrix.Translation((cx, cy, 0.0)) @ Matrix.Rotation(R.uniform(0, 6.28), 4, 'Z'))
        uvl = bm.loops.layers.uv.new('UVMap'); FK._uv_project(bm.faces, uvl, Matrix.Identity(4), None, (k * 0.3, 0)); FK._merge(pc, bm, 'rub')
        for j in range(5):   # fallen slates and a stone or two on each heap
            p = Vector((cx + R.uniform(-r, r) * 0.8, cy + R.uniform(-r, r) * 0.7, h * 0.55))
            FK.add_box(pc, 'slate', (R.uniform(0.3, 0.45), R.uniform(0.22, 0.32), 0.035), Matrix.Translation(p) @ Matrix.Rotation(R.uniform(0, 6.28), 4, 'Z') @
                       Matrix.Rotation(R.uniform(-0.5, 0.5), 4, 'X'), cham=0.008, uv_world=True, seed=R.random() * 40)
        FK.add_box(pc, 'stone', (0.5, 0.36, 0.3), Matrix.Translation((cx + 0.2, cy - 0.3, h * 0.5)) @ Matrix.Rotation(R.uniform(0, 3), 4, 'Z') @ Matrix.Rotation(0.3, 4, 'X'), cham=0.05, segs=1, uv_world=True, seed=k + 3)
    for k, (a, b) in enumerate((((1.5, 0.6, 0.3), (3.4, 1.7, 0.5)), ((2.4, -1.2, 0.25), (4.2, -0.3, 0.2)), ((5.2, 2.6, 0.2), (6.5, 2.0, 0.3)))):
        lean_member(pc, 'oakw', a, b, 0.22, 0.26, name='fallen%d' % k, up=(0, 0, 1))
    return pc

# ----------------------------------------------------------------------------- assembly
def build_state(ruin):
    tp = tops(ruin); op = opens_for(ruin)
    pc = piece('Shell' if ruin else 'Frame')
    for key, W in (('S', W_S), ('N', W_N), ('W', W_W), ('E', W_E)):
        z1 = CT + 0.1 if key in 'SN' else RZ
        p, F = wall('Wall' + key + ('R' if ruin else ''), W['F'], W['L'], tp[key], op[key], ruin, z1=z1)
        merge(pc, p, F)
    for p, F in porch(ruin): merge(pc, p, F)
    q = piece('Dressings' + ('R' if ruin else ''))
    plinth(q, ruin); quoins(q, ruin, tp); buttresses(q, ruin, tp); cornice(q, ruin, tp); copings(q, ruin); bellcote(q, ruin); porch_world(q, ruin)
    flat(q, 'flag', [(-HX + 0.7, -HY + 0.7, 0.03), (HX - 0.7, -HY + 0.7, 0.03), (HX - 0.7, HY - 0.7, 0.03), (-HX + 0.7, HY - 0.7, 0.03)], (0, 0, 1), 4, 3)
    merge(pc, q)
    merge(pc, roof(ruin))
    if ruin: merge(pc, heaps())
    return pc

def _clamp01(v): return max(0.0, min(1.0, v))
def _streak(p, fx=6.0, fz=0.5):
    return _clamp01((noise.noise(Vector((p.x * fx, p.y * fx, p.z * fz + 3.1))) + 0.1) * 2.2)

STONE_MATS = ('FK_Sandstone', 'KH_Rubble', 'FK_PlinthJoint', 'FK_ChimneyStone', 'KH_Flag')
SOFT = {'KH_Rubble'}

def write_wear(ob):
    me = ob.data; attr = me.color_attributes.new('wear', 'FLOAT_COLOR', 'CORNER'); mw = ob.matrix_world
    shell = ob.name == 'Shell'
    for poly in me.polygons:
        n = (mw.to_3x3() @ poly.normal).normalized()
        mname = me.materials[poly.material_index].name
        stone = mname in STONE_MATS; slate = mname == 'KH_Slate'
        c = mw @ poly.center
        sv = 0.08 + 0.26 * _clamp01(0.5 + 1.2 * noise.noise(Vector((c.x * 2.6 + 7, c.y * 2.6, c.z * 3.2))) + 0.6 * noise.noise(c * 0.55 + Vector((1, 4, 2))))
        for li in poly.loop_indices:
            p = mw @ me.vertices[me.loops[li].vertex_index].co
            nz = noise.noise(p * 1.7); st = _streak(p)
            r = max(0.0, 1 - p.z / (1.0 + 0.3 * nz)) ** 1.2 * 0.75
            r = max(r, 0.1 * max(0.0, noise.noise(p * 0.5 + Vector((3, 1, 2)))) + (0.22 if shell else 0.0))
            g = 0.0
            wall_ = abs(n.z) < 0.7
            if n.z > 0.5 and p.z < 0.7: g = max(g, 0.3 + 0.3 * nz)                 # moss on plinth tops and the floor
            if p.y < -0.5 and wall_: g = max(g, 0.18 * max(0.0, nz + 0.5) * _clamp01(1 - p.z / 6.0))   # the shaded back wall greens
            if shell: g = max(g, 0.3 * max(0.0, nz + 0.4))
            if stone:
                r = max(sv + (0.08 if shell else 0.0) + 0.04 * nz, r if p.z < 0.6 else 0.0)
                if wall_:
                    foot = _clamp01(1 - max(p.z, 0.0) / 1.0) ** 0.55
                    r = max(r, 0.8 * foot * (0.5 + 0.5 * st), 0.3 * _clamp01(1 - p.z / 1.5) * st)
                    for zw in (WIN['z0'],):   # sill run-off below each window
                        if 0.2 < zw - p.z < 1.3 and st > 0.2: r = max(r, 0.5 * st * _clamp01(1 - (zw - p.z) / 1.3))
                    if 4.9 < p.z < CT + 0.4: r = max(r, 0.4 * _clamp01((p.z - 4.9) / 0.5) * (0.5 + 0.5 * st))   # soot under the cornice
                r = min(1.0, r * 1.5)
            if slate:
                tv = ((poly.index * 2654435761) % 1009) / 1009.0
                r = max(0.08 * tv, 0.15 * _clamp01(1 - (p.z - 5.0) / 1.2))
                if n.y * (1 if p.y > 0 else -1) > 0.1 or p.y < -0.3: g = max(g, 0.5 * _clamp01(0.5 + nz) * _clamp01(1.0 - (p.z - 5.3) / 4.5))   # moss low on the roof, more on the north slope
                if p.y > 0.3: g *= 0.4
            if mname == 'KH_Limewash': r = max(r, 0.3)
            b = 0.0
            if ob.name == 'Part_Door': b = 0.4 * max(0.0, 1 - abs(p.z - 1.1) / 0.5); r = min(r, 0.3); g = 0.0
            attr.data[li].color = (min(1.0, r), min(1.0, g), b, 1.0)

def to_object(pc, parent, origin=None):
    if origin is not None:
        bmesh.ops.translate(pc.bm, verts=pc.bm.verts[:], vec=-origin)
    me = bpy.data.meshes.new(pc.name); pc.bm.normal_update(); pc.bm.to_mesh(me); pc.bm.free()
    for m in pc.mats: me.materials.append(m)
    for poly in me.polygons:
        if me.materials[poly.material_index].name in SOFT: poly.use_smooth = True
    ob = bpy.data.objects.new(pc.name, me); bpy.context.scene.collection.objects.link(ob); ob.parent = parent
    if origin is not None: ob.location = origin
    bpy.context.view_layer.update(); write_wear(ob)
    return ob

def tris(ob): return sum(len(p.vertices) - 2 for p in ob.data.polygons)

def build():
    root = bpy.data.objects.new('KeepHall', None); bpy.context.scene.collection.objects.link(root)
    shell = to_object(build_state(True), root); frame = to_object(build_state(False), root)
    print('  Shell %d tris (cap 25000), %d slots' % (tris(shell), len(shell.data.materials)))
    pc = door_part()
    FW = W_W['F']; bmesh.ops.transform(pc.bm, matrix=FW, verts=pc.bm.verts[:])
    s = FW @ Vector((DOOR_U, 0, DZ0 - 0.0))
    s = Vector((HX, PY, 0.0))
    door = to_object(pc, root, origin=s)
    e = bpy.data.objects.new('SOCKET_Door', None); bpy.context.scene.collection.objects.link(e); e.parent = frame; e.location = s
    door.parent = root
    me = bpy.data.meshes.new('FOOTPRINT'); bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1.0)
    bm.transform(Matrix.Translation((0, 0, 0.05)) @ Matrix.Diagonal((9.0, 6.0, 0.1, 1))); bm.to_mesh(me); bm.free()
    fp = bpy.data.objects.new('FOOTPRINT', me); bpy.context.scene.collection.objects.link(fp); fp.parent = root; fp.display_type = 'WIRE'; fp.hide_render = True
    objs = [frame, door]; total = tris(frame) + tris(door)
    for o in [shell] + objs:
        xs = [(o.matrix_world @ Vector(c)) for c in o.bound_box]
        print('  bounds %-10s x %.2f..%.2f y %.2f..%.2f z %.2f..%.2f  %d tris' % ((o.name,) + (min(v.x for v in xs), max(v.x for v in xs), min(v.y for v in xs), max(v.y for v in xs), min(v.z for v in xs), max(v.z for v in xs)) + (tris(o),)))
    print('KeepHall: Shell %d, Frame+parts %d tris (caps 25000 / 35000); slots Shell %s' % (tris(shell), total, [m.name for m in shell.data.materials]))
    print('  Frame slots %s | Door slots %s' % ([m.name for m in frame.data.materials], [m.name for m in door.data.materials]))
    return root, shell, objs

# ----------------------------------------------------------------------------- review
TOWER_AT = Vector((-8.6, 0.5, 0.0))      # bell tower centre relative to the hall (Unity terrace layout: x 5.6, z 24.0 vs hall -3.0, 24.5)
VIEWS_DEF = {
    'sq': (Vector((3.0, 20.0, 3.4)), Vector((-3.5, 0.0, 6.6)), 54),
    'tq': (Vector((15.0, 13.0, 4.0)), Vector((-4.5, 0.0, 6.8)), 54),
    'back': (Vector((-13.0, -15.0, 4.5)), Vector((0.5, 0.0, 5.8)), 50),
    'frame': (Vector((12.0, 11.0, 3.6)), Vector((1.0, 0.0, 4.6)), 46),
    'shell': (Vector((12.0, 11.0, 3.6)), Vector((1.0, 0.0, 4.6)), 46),
    'shellb': (Vector((11.0, -12.0, 3.6)), Vector((0.5, 0.0, 4.2)), 46),
    'door': (Vector((10.5, -5.2, 2.2)), Vector((4.9, -0.5, 1.9)), 44),
    'pair': (Vector((2.0, 26.0, 4.5)), Vector((-3.0, 0.0, 8.0)), 60),
}

def review(root, shell, objs):
    sc = bpy.context.scene; FK.RRES = RRES; FK.SAMPLES = SAMPLES
    cam = FK.review_scene(root, variant=True)
    sc.render.resolution_x = RRES; sc.render.resolution_y = int(RRES * 9 / 16)
    tower = None
    bpy.ops.mesh.primitive_cylinder_add(radius=0.2, depth=1.75, location=(6.2, -0.5, 0.875))
    fig = bpy.context.object; fig.name = 'Figure175'
    fm = bpy.data.materials.new('Figure'); fm.use_nodes = True
    fm.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value = (0.25, 0.18, 0.12, 1)
    fig.data.materials.append(fm)
    import sc_bell_tower as BT
    troot = BT.build_tower(); troot.location = TOWER_AT
    tobjs = [o for o in bpy.context.scene.objects if o.name.startswith('Tower_')]
    for v in VIEWS:
        frm, to, fov = VIEWS_DEF[v]
        isshell = v.startswith('shell')
        shell.hide_render = not isshell
        for o in objs: o.hide_render = isshell
        if v == 'frame': objs[1].hide_render = True
        for o in tobjs: o.hide_render = v in ('door',)
        fig.hide_render = v not in ('door', 'sq', 'tq')
        FK.look(cam, frm, to); cam.data.angle = math.radians(fov)
        sc.render.filepath = os.path.join(OUT, 'hall_%s.png' % v)
        bpy.ops.render.render(write_still=True)
        print('rendered', v)

if __name__ == '__main__':
    bpy.ops.wm.read_factory_settings(use_empty=True)
    root, shell, objs = build()
    if '--export' in argv:
        for o in bpy.context.scene.objects: o.select_set(o == root or o.parent in (root, objs[0]))
        bpy.ops.export_scene.fbx(filepath=os.path.join(OUT, 'KeepHall.fbx'), use_selection=True, axis_forward='-Z', axis_up='Y', apply_scale_options='FBX_SCALE_UNITS',
                                 bake_space_transform=True, object_types={'EMPTY', 'MESH'}, mesh_smooth_type='FACE', add_leaf_bones=False, colors_type='LINEAR', path_mode='STRIP')
    if '--blend' in argv:
        bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'keephall.blend'))
    if '--no-render' not in argv:
        review(root, shell, objs)
