# Tobin hair, rebuild 2026-11-10: a modelled hair SHELL that lies on the skull like a sheet + authored wispy LOCK CARDS on top.
#   blender -b --factory-startup -P build_shell_hair.py -- <out_dir> [tex=<dir with Hair_Cards_*/Hair_Shell_* png>] [preview=1] [seed=7]
# Writes <out_dir>/Tobin_shell_hair.blend (objects Tobin_HairShell + Tobin_Hair (cards) + the MetaPerson head) and, with preview=1, prev_<view>.png.
#
# Shell: a lat/long grid whose pole sits at the crown whorl (top, a little back), so grid meridians run the way hair grows (forward to the fringe,
# down the sides and back). Each grid direction is ray-cast from outside onto the head (outermost hit, so ears are covered), the radius field is
# dilated + blurred so the sheet bridges the ears instead of following them, then a thickness profile is added (thin at the hairline, thick on
# the crown), the sides below ear level hang down and flare instead of hugging the cheek, and the lower edge is cut per world azimuth
# (hairline / temples / in front of the ears / over the ears / nape) with one pointed tooth per lock. Lock clumps are geometry bumps along
# the swirled meridians (the fringe sector sweeps to the character's right, part a little to the character's left). Repeated smooth + BVH push
# keeps >= CLEAR from head, eyelashes and eyeballs. Closed slab (outer, inner, rim) so it reads solid from below; every face oriented by the
# nearest head normal.
# Cards: guide curves walk along the shell flow (constant swirled azimuth), leave it at the edge and continue with gravity / sweep / flick,
# with a BVH clearance push; tapered ribbon (3 verts across, bulged) mapped to one of 8 lock variants of the alpha atlas, plus a reversed copy
# 0.5 mm inward so locks do not vanish from behind.
import bpy, bmesh, numpy as np, sys, os, math
from mathutils import Vector, Matrix
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index('--') + 1:]; OUT = argv[0]; kw = dict(x.split('=', 1) for x in argv[1:])
TEX = kw.get('tex', 'C:/AI/test_out/heroes/tobin/hair_v8/tex2'); PREVIEW = kw.get('preview', '1') == '1'
rng = np.random.default_rng(int(kw.get('seed', 7)))
F = lambda k, d: float(kw.get(k, d))
os.makedirs(OUT, exist_ok=True)
CLMP = kw.get('clumps', '1') == '1'   # FF / KH style solid clump locks (default) vs alpha strand cards
NDEF = lambda k, clump, card: int(kw.get(k, clump if CLMP else card))

bpy.ops.wm.read_factory_settings(use_empty=True)
# the project's final head: TobinBase.fbx (the older Tobin_Base_v2_sliders.glb has a 2.4 cm smaller back of the skull / nape -> the shell sat inside the real head)
HEAD_SRC = kw.get('head', os.path.join(os.path.dirname(os.path.abspath(__file__)), '../../../../../../Assets/_Project/Content/Characters/TobinBase/TobinBase.fbx'))
(bpy.ops.import_scene.fbx if HEAD_SRC.lower().endswith('.fbx') else bpy.ops.import_scene.gltf)(filepath=os.path.normpath(HEAD_SRC))
for _o in bpy.data.objects:   # FBX comes without textures (path_mode STRIP): plain skin colour for the previews
    if _o.type == 'MESH' and _o.name.startswith('Avatar'):
        for _m in _o.data.materials:
            if _m and _m.use_nodes:
                for _n in [n for n in _m.node_tree.nodes if n.type == 'TEX_IMAGE' and not (n.image and n.image.has_data)]: _m.node_tree.nodes.remove(_n)
                _b = _m.node_tree.nodes.get('Principled BSDF')
                if _b: _b.inputs['Base Color'].default_value = (0.80, 0.56, 0.44, 1) if 'Eye' not in _o.name else (0.9, 0.9, 0.9, 1)
dg = bpy.context.evaluated_depsgraph_get()
def world_bm(names):
    bm = bmesh.new()
    for n in names:
        o = bpy.data.objects[n]; t = bmesh.new(); t.from_object(o, dg); t.transform(o.matrix_world)
        me = bpy.data.meshes.new('tmp'); t.to_mesh(me); t.free(); bm.from_mesh(me); bpy.data.meshes.remove(me)
    bm.faces.ensure_lookup_table(); return bm
bm_head = world_bm(['AvatarHead']); TREE_HEAD = BVHTree.FromBMesh(bm_head)
bm_clr = world_bm(['AvatarHead', 'AvatarEyelashes', 'AvatarLeftEyeball', 'AvatarRightEyeball']); TREE = BVHTree.FromBMesh(bm_clr)

C = Vector((0.0, -0.045, 1.735))                 # skull centre (probe: forehead y -0.144, back of skull y +0.06)
WD = Vector((0.0, F('whorl_y', 0.42), 1.0)).normalized()   # crown whorl direction (pole of the grid)
E1 = (Vector((0, -1, 0)) - WD * Vector((0, -1, 0)).dot(WD)).normalized(); E2 = WD.cross(E1)   # a=0 -> front, a=+90deg -> +x (character's left)
CLEAR = F('clear', 0.004)
A_PART = F('part', 0.30)                          # part meridian, a little to the character's left (+x)
NL = int(kw.get('locks', 44))                     # locks around the head (multiple of 4 = locks per texture tile)
K_SW = F('sweep', 0.85)                           # fringe sweep to the character's right (rad of azimuth at the edge)

def sstep(e0, e1, x): t = np.clip((np.asarray(x, float) - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)
def wrap(a): return (np.asarray(a) + np.pi) % (2 * np.pi) - np.pi
def dirs(a, p):
    a = np.asarray(a, float); p = np.asarray(p, float)
    w, e1, e2 = np.array(WD), np.array(E1), np.array(E2)
    return (np.cos(p)[..., None] * w + np.sin(p)[..., None] * (np.cos(a)[..., None] * e1 + np.sin(a)[..., None] * e2))
def phi_of(P): return np.degrees(np.arctan2(P[..., 0] - C.x, -(P[..., 1] - C.y)))   # world azimuth: 0 front, +90 character's left
def swirl(a, t):
    aw = wrap(a); s = sstep(0.30, 1.0, t)
    return np.where(aw < A_PART, K_SW * s * np.exp(-((aw + 0.15) / 0.95) ** 2), -0.22 * s * np.exp(-((aw - A_PART) / 0.6) ** 2))

# ---------------- 1. radius field on a fine (p, a) grid ----------------
NA = int(kw.get('cols', 128)); PF = np.linspace(0.0, 2.55, 150); AS = np.linspace(-np.pi, np.pi, NA, endpoint=False)
AA, PP = np.meshgrid(AS, PF); D = dirs(AA, PP)
rhit = np.zeros(AA.shape)
for i in range(AA.shape[0]):
    for j in range(NA):
        d = Vector(D[i, j]); o = C + d * 0.40
        loc, nrm, idx, dist = TREE_HEAD.ray_cast(o, -d, 0.45)
        rhit[i, j] = (loc - C).length if loc is not None else 0.0
# dilate + blur (periodic in a) -> sheet bridges ears / nose bridge bumps
def blur(r, it):
    for _ in range(it):
        r = (np.roll(r, 1, 1) + np.roll(r, -1, 1) + r) / 3.0
        r = np.vstack([r[:1], (r[:-2] + r[1:-1] + r[2:]) / 3.0, r[-1:]])
    return r
r1 = rhit.copy()
for _ in range(int(kw.get('dil', 2))):
    r1 = np.maximum.reduce([r1, np.roll(r1, 1, 1), np.roll(r1, -1, 1), np.vstack([r1[:1], r1[:-1]]), np.vstack([r1[1:], r1[-1:]])])
renv = np.maximum(blur(r1, 10), rhit + 0.002)
# thickness profile: elevation of the direction (thin low, thick on the crown)
elev = np.arcsin(np.clip(D[..., 2], -1, 1))
thk = F('t0', 0.006) + F('t1', 0.027 if CLMP else 0.032) * sstep(-0.35, 1.0, elev) + F('tback', 0.008) * np.exp(-((elev - 0.45) / 0.35) ** 2) * sstep(0.1, 0.6, D[..., 1])   # rounder crown / occiput bulge behind
_phd = np.degrees(np.arctan2(D[..., 0], -D[..., 1])); _zap = C.z + D[..., 2] * 0.10
_wfl = np.exp(-(_phd / 50.0) ** 2) * sstep(1.80, 1.765, _zap)   # front, below the hairline: the fringe lies close on the forehead
thk = thk * (1.0 - 0.75 * _wfl) * (1.0 - 0.30 * np.exp(-(_phd / 40.0) ** 2) * sstep(0.85, 0.35, elev))   # less forward bulge over the brow (side-view visor)
thk = thk + F('tcrown', 0.0) * sstep(0.75, 1.0, elev) + F('tpuff', 0.013) * np.exp(-((elev - 0.35) / 0.25) ** 2) * sstep(45, 75, np.abs(_phd)) * sstep(150, 120, np.abs(_phd))   # puff above the ears: widest at temple height
renv = renv * (1 - _wfl) + (rhit + 0.0025) * _wfl   # thin where the shell runs down the forehead (fringe)
P_f = C[:] + D * (renv + thk)[..., None]
P_f = np.asarray(P_f)
# sides below ear level hang down and flare (no cheek hugging)
ZREF = F('zref', 1.712); FLARE = F('flare', -0.06)
for j in range(NA):
    col = P_f[:, j]
    k = np.where(col[:, 2] < ZREF)[0]
    if len(k) == 0: continue
    k0 = k[0]; ref = col[k0]; rho0 = math.hypot(ref[0] - C.x, ref[1] - C.y)
    for i in k:
        p = col[i]; h = np.array([p[0] - C.x, p[1] - C.y]); rho = np.linalg.norm(h)
        aph = abs(phi_of(p)); ws = float(sstep(45, 70, aph))
        fl = float(np.interp(aph, [50, 75, 100, 130, 180], [FLARE, F('flare_ear', -0.45), -0.45, -0.8, -0.9]))   # over the ears: hang ~straight; back: taper into the nape
        want = rho0 + fl * (ZREF - p[2])
        h = h / max(rho, 1e-6) * (rho + (want - rho) * ws)   # clearance push afterwards keeps it off the neck
        # flaps hang: blend the drop toward vertical below ZREF
        P_f[i, j, 0] = C.x + h[0]; P_f[i, j, 1] = C.y + h[1]

# ---------------- 2. lower edge per column + teeth ----------------
# signed world azimuth (deg, + = character's left): fringe low on the character's right (-), shorter left of the part, temples, in front of / over the ears, nape
EDGE = [(-180, 1.666), (-155, 1.688), (-135, 1.664), (-112, 1.644), (-92, 1.640), (-78, 1.652), (-60, 1.700), (-48, 1.716), (-34, 1.728), (-18, 1.740), (-4, 1.752), (8, 1.768), (20, 1.776), (32, 1.765),
        (45, 1.735), (60, 1.702), (78, 1.652), (92, 1.640), (112, 1.644), (135, 1.664), (155, 1.688), (180, 1.666)]
def zedge(ph):
    xs, ys = zip(*EDGE); return float(np.interp(float(ph), xs, ys))
pedge = np.zeros(NA)
for j in range(NA):
    col = P_f[:, j]; pe = PF[-1]
    for i in range(10, len(PF)):
        if col[i, 2] < zedge(phi_of(col[i])):
            z0 = col[i - 1, 2] - zedge(phi_of(col[i - 1])); z1 = col[i, 2] - zedge(phi_of(col[i]))
            pe = PF[i - 1] + (PF[i] - PF[i - 1]) * z0 / max(z0 - z1, 1e-9); break
    pedge[j] = pe
pedge = (np.roll(pedge, 1) + pedge + np.roll(pedge, -1)) / 3
lock_amp = rng.uniform(0.35, 1.0, NL)
WPH = rng.uniform(0, 2 * np.pi, 3)
def lock_x(ap, t=1.0):   # continuous lock coordinate (lock units), warped so locks vary in width and wander a little down their length
    ap = np.asarray(ap, float); t = np.asarray(t, float)
    w = 0.85 * np.sin(3 * ap + WPH[0] + 1.3 * t) + 0.5 * np.sin(7 * ap + WPH[1] - 2.1 * t) + 0.3 * np.sin(13 * ap + WPH[2] + 3.0 * t)
    return (wrap(ap) + np.pi) / (2 * np.pi) * NL + w
def lock_frac(ap, t=1.0):  # -> (lock index, fraction across the lock 0..1)
    x = lock_x(ap, t); k = np.floor(x).astype(int) % NL; return k, x - np.floor(x)
lock_pk = rng.uniform(0.3, 0.7, NL)   # where the tooth tip sits across each lock
# teeth: one point per lock, amplitude by sector (soft at the front hairline, long on the sides and nape)
ph_col = np.array([phi_of(P_f[min(int(np.searchsorted(PF, pedge[j])), len(PF) - 1), j][None])[0] for j in range(NA)])
tooth_rad = np.interp(np.abs(ph_col), [0, 35, 55, 80, 180], [F('tooth_f', 0.12), 0.10, 0.16, 0.22, F('tooth_b', 0.21)])
k_col, f_col = lock_frac(AS + swirl(AS, 1.0))
pk = lock_pk[k_col]; tri = np.where(f_col < pk, f_col / pk, (1 - f_col) / (1 - pk)) ** 1.5
pedge_t = pedge + tooth_rad * lock_amp[k_col] * tri

# ---------------- 3. final grid ----------------
NR = int(kw.get('rows', 44)); TT = np.linspace(0.025, 1.0, NR) ** 0.85
P = np.zeros((NR, NA, 3)); PREL = np.zeros((NR, NA))
for j in range(NA):
    ps = TT * pedge_t[j]
    for c in range(3): P[:, j, c] = np.interp(ps, PF, P_f[:, j, c])
pole = C + WD * float(np.interp(0.0, PF, (renv + thk)[:, 0]))
def push(P, clear):
    n = 0
    for i in range(P.shape[0]):
        for j in range(P.shape[1]):
            v = Vector(P[i, j]); loc, nr, _, d = TREE.find_nearest(v, 0.2)
            if loc is None: continue
            s = (v - loc).dot(nr)
            if s < clear: P[i, j] = np.array(loc + nr * clear); n += 1
            # sides: horizontal ray from outside toward the head axis; if the outermost head hit (ear rim) lies outside the vertex, move the vertex out past it
            p = P[i, j]; h = np.array([p[0] - C.x, p[1] - C.y]); rho = np.linalg.norm(h)
            if 1.60 < p[2] < 1.76 and abs(phi_of(p)) > 45 and rho > 1e-4:
                u = h / rho; o = Vector((C.x + u[0] * 0.4, C.y + u[1] * 0.4, p[2])); hit = TREE.ray_cast(o, Vector((-u[0], -u[1], 0.0)), 0.4)[0]
                if hit is not None:
                    rh = math.hypot(hit.x - C.x, hit.y - C.y)
                    cr = clear + 0.004   # extra margin: the shell is a coarse grid, the ear rim must not poke through between vertices
                    if rho < rh + cr: P[i, j, 0] = C.x + u[0] * (rh + cr); P[i, j, 1] = C.y + u[1] * (rh + cr); n += 1
    return n
for it in range(6):
    Q = P.copy()
    wu = (1.0 - sstep(0.55, 0.85, TT[1:-1]))[:, None, None]   # no sideways smoothing near the edge: keeps the lock teeth sharp
    Q[1:-1] = 0.5 * P[1:-1] + 0.25 * (P[:-2] + P[2:]) * (1 - wu) + 0.125 * (P[:-2] + P[2:] + np.roll(P[1:-1], 1, 1) + np.roll(P[1:-1], -1, 1)) * wu
    Q[-1] = P[-1]
    P = Q; nmov = push(P, CLEAR + 0.002)
print('SHELL smooth/push moved', nmov)
if kw.get('dbg'):
    for jj in [0, NA//4, NA*3//8, NA//2, NA*5//8]:
        print('DBG col', jj, 'phi', phi_of(P[-1, jj][None]).round(0), [(round(float(P[i,jj,2]),3), round(math.hypot(P[i,jj,0]-C.x,P[i,jj,1]-C.y),3)) for i in range(NR-14, NR, 3)])
        print('DBG fine', [(round(float(P_f[i,jj,2]),3), round(math.hypot(P_f[i,jj,0]-C.x,P_f[i,jj,1]-C.y),3)) for i in range(60, 150, 12)])
for lo, hi in [(-30, 0), (0, 30), (60, 120), (-120, -60), (150, 210)]:
    phs = phi_of(P[-1]); m = ((phs >= lo) & (phs < hi)) | ((phs + 360 >= lo) & (phs + 360 < hi)); print('EDGE z', lo, hi, P[-1][m][:, 2].min().round(3) if m.any() else '-', P[-1][m][:, 2].max().round(3) if m.any() else '-')
def grid_normals(P):
    du = np.roll(P, -1, 1) - np.roll(P, 1, 1)
    dv = np.vstack([P[1:2] - P[0:1], P[2:] - P[:-2], P[-1:] - P[-2:-1]])
    n = np.cross(dv, du); n /= np.linalg.norm(n, axis=2, keepdims=True) + 1e-12
    out = P - np.array(C); sgn = np.sign((n * out).sum(2, keepdims=True)); return n * np.where(sgn == 0, 1, sgn)
N0 = grid_normals(P); PB = P.copy()
# lock bumps along swirled meridians + part groove
AP = np.zeros((NR, NA))
for i in range(NR): AP[i] = AS + swirl(AS, TT[i])
kk, ff = lock_frac(AP, np.tile(TT[:, None], (1, NA)))
bump = np.sqrt(np.clip(1 - (2 * ff - 1) ** 2, 0, 1)) - 0.62
elevG = np.arcsin(np.clip(N0[..., 2], -1, 1))
amp = F('bump', 0.0065) * lock_amp[kk] * (0.55 + 0.45 * sstep(-0.3, 0.8, elevG)) * sstep(0.0, 0.12, TT)[:, None]
part = np.exp(-((wrap(np.tile(AS, (NR, 1)) - A_PART)) / 0.045) ** 2) * sstep(0.25, 0.55, TT)[:, None] * (np.abs(wrap(np.tile(AS, (NR, 1)))) < 1.2)
P = P + N0 * (amp * bump - F('partd', 0.006) * part)[..., None]
push(P, CLEAR)
N1 = grid_normals(P)
# inner surface: toward the head, at most 9 mm under the outer one, never closer than 1.5 mm to the skin
PI = np.zeros_like(P)
for i in range(NR):
    for j in range(NA):
        v = Vector(PB[i, j]); loc, nr, _, d = TREE.find_nearest(v, 0.2); s = (v - loc).dot(nr) if loc is not None else 0.01
        PI[i, j] = PB[i, j] - N0[i, j] * float(np.clip(s - 0.0015, 0.0015, F('slab', 0.005)))
for i in range(NR):   # inner surface never inside the head (nearest-point push only)
    for j in range(NA):
        v = Vector(PI[i, j]); loc, nr, _, d = TREE.find_nearest(v, 0.2)
        if loc is not None and (v - loc).dot(nr) < 0.0012: PI[i, j] = np.array(loc + nr * 0.0012)
pole_in = C + WD * (float(np.interp(0.0, PF, renv[:, 0])) + 0.0015)

def build_shell():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap')
    NT = NL // 4
    def uv_of(i, j):
        jj = j % NA; a = AS[jj] + (2 * np.pi if j == NA else 0); ap = AS[jj] + swirl(AS[jj], TT[i]); x = float(lock_x(ap, TT[i])) + (NL if j == NA else 0); return (x / 4.0, 1.0 - TT[i] * 0.98)
    GAP = lambda i: (0.003, 1.0 - TT[i] * 0.98)   # inner surface + rim sample the dark gap between texture locks (shadowed hair interior, no stripes)
    VO = [[bm.verts.new(P[i, j]) for j in range(NA)] for i in range(NR)]; VI = [[bm.verts.new(PI[i, j]) for j in range(NA)] for i in range(NR)]
    po = bm.verts.new(pole); pi_ = bm.verts.new(pole_in)
    def face(vs, uvs):
        try: f = bm.faces.new(vs)
        except ValueError: return None
        for l, uv in zip(f.loops, uvs): l[uvl].uv = uv
        return f
    outer, inner = [], []
    for j in range(NA):
        j1 = (j + 1) % NA
        outer.append(face([po, VO[0][j1], VO[0][j]], [(uv_of(0, j)[0], 1.0), uv_of(0, j + 1), uv_of(0, j)]))
        inner.append(face([pi_, VI[0][j], VI[0][j1]], [GAP(0), GAP(0), GAP(0)]))
        for i in range(NR - 1):
            outer.append(face([VO[i][j], VO[i][j1], VO[i + 1][j1], VO[i + 1][j]], [uv_of(i, j), uv_of(i, j + 1), uv_of(i + 1, j + 1), uv_of(i + 1, j)]))
            inner.append(face([VI[i][j], VI[i + 1][j], VI[i + 1][j1], VI[i][j1]], [GAP(i), GAP(i + 1), GAP(i + 1), GAP(i)]))
        # rim (lower edge): strip of dark gap texture
        face([VO[NR - 1][j], VO[NR - 1][j1], VI[NR - 1][j1], VI[NR - 1][j]], [GAP(NR - 1)] * 4)
    bm.normal_update()
    # orient: outer faces away from the head (nearest head normal), inner faces toward it
    fl = 0
    outer_set = set(f for f in outer if f); inner_set = set(f for f in inner if f)
    for f in bm.faces:
        c = f.calc_center_median(); loc, nr, _, d = TREE.find_nearest(c, 0.3)
        if f in outer_set: bad = f.normal.dot(nr) < 0
        elif f in inner_set: bad = f.normal.dot(nr) > 0
        else: bad = f.normal.dot(c - C) < 0
        if bad: f.normal_flip(); fl += 1
    bm.normal_update(); print('SHELL faces', len(bm.faces), 'flipped', fl)
    me = bpy.data.meshes.new('HairShell'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    # inner surface + rim get the OUTER normals: seen from below (under the side flaps) they light like hair, not as black inward-lit steps
    nrm = [tuple(N1[i, j]) for i in range(NR) for j in range(NA)] * 2 + [tuple(WD), tuple(WD)]
    me.normals_split_custom_set_from_vertices(nrm)
    ob = bpy.data.objects.new('Tobin_HairShell', me); bpy.context.scene.collection.objects.link(ob); return ob

shell = build_shell()

# ---------------- 4. lock cards ----------------
def shell_at(a, t):
    """outer shell point (base, before bumps) + normal at azimuth a (rad), row parameter t (0..1, may exceed 1 -> clamps)."""
    t = float(np.clip(t, TT[0], 1.0)); x = (wrap(a) + np.pi) / (2 * np.pi) * NA; j0 = int(np.floor(x)) % NA; j1 = (j0 + 1) % NA; fx = x - np.floor(x)
    i0 = int(np.clip(np.searchsorted(TT, t) - 1, 0, NR - 2)); fy = (t - TT[i0]) / (TT[i0 + 1] - TT[i0])
    def L(A): return (A[i0, j0] * (1 - fx) + A[i0, j1] * fx) * (1 - fy) + (A[i0 + 1, j0] * (1 - fx) + A[i0 + 1, j1] * fx) * fy
    n = L(N1); return L(P), n / (np.linalg.norm(n) + 1e-12)
def solve_a(ap, t):
    a = ap
    for _ in range(4): a = ap - float(swirl(a, t))
    return a
_bs = bmesh.new(); _vv = [[_bs.verts.new(P[i, j]) for j in range(NA)] for i in range(NR)]
for i in range(NR - 1):
    for j in range(NA): _bs.faces.new([_vv[i][j], _vv[i][(j + 1) % NA], _vv[i + 1][(j + 1) % NA], _vv[i + 1][j]])
TREE_SHELL = BVHTree.FromBMesh(_bs); _bs.free()
def clearance_push(p, c, cs=0.0):
    v = Vector(p); loc, nr, _, d = TREE.find_nearest(v, 0.3)
    if loc is not None:
        s = (v - loc).dot(nr)
        if s < c: v = loc + nr * c
    if cs > 0:   # stay on top of the shell (only where the point is over its surface, not beyond its edge)
        loc, nr, _, d = TREE_SHELL.find_nearest(v, 0.3)
        if loc is not None:
            if nr.dot(loc - C) < 0: nr = -nr
            off = v - loc; s = off.dot(nr)
            if s < cs and (d < 1e-6 or abs(s) > 0.7 * off.length): v = loc + nr * cs
    return np.array(v)
def grow(ap, t0, t1, ext, lift, steer, ds=0.004, rooted=0.003, minc=0.005, tiplift=0.0, cs=0.0, raw=False, zstop=-1.0, arch=0.0):
    """on-shell part: constant swirled azimuth ap from t0 to t1 at `lift` above the shell (root starts `rooted` inside),
    then `ext` metres more in 3D, steered by steer(s, pos, dir) -> acceleration; clearance >= minc."""
    pts = []
    ts = np.linspace(t0, t1, max(3, int((t1 - t0) * 14) + 2))
    for k, t in enumerate(ts):
        a = ap if raw else solve_a(ap, t); q, n = shell_at(a, t); h = -rooted + (lift + rooted) * float(sstep(0, 0.35, k / max(len(ts) - 1, 1))) + tiplift * (k / max(len(ts) - 1, 1)) ** 2 + arch * math.sin(math.pi * min(1.0, 1.3 * k / max(len(ts) - 1, 1)))
        pts.append(q + n * h)
    pts = [np.append(np.array(x), 0.0) for x in pts]   # 4th column: 'free' (0 = rides the rigid shell, 1 = swings on its chain)
    d = pts[-1][:3] - pts[-2][:3]; d /= np.linalg.norm(d) + 1e-12; p = pts[-1][:3].copy(); s = 0.0
    while s < ext and p[2] > zstop:
        acc = steer(s / max(ext, 1e-6), p, d); d = d + acc * ds; d /= np.linalg.norm(d); p = p + d * ds; p = clearance_push(p, minc, cs); pts.append(np.append(p, float(sstep(0.0, 0.03, s)))); s += ds
    return np.array(pts)
def resample(pts, n):
    L = np.concatenate([[0], np.cumsum(np.linalg.norm(np.diff(pts[:, :3], axis=0), axis=1))]); u = np.linspace(0, L[-1], n)
    return np.stack([np.interp(u, L, pts[:, c]) for c in range(pts.shape[1])], 1), L[-1]

cards = []   # (pts, width_root, variant, taper)
def add(pts, w, var=None, tw=1.0):
    if len(pts) < 3: return
    pts = np.asarray(pts, float)
    if pts.shape[1] == 3: pts = np.hstack([pts, np.linspace(0, 0.6, len(pts))[:, None]])
    cards.append((pts, w * F('wscale', 2.2 if CLMP else 1.15) * (rng.uniform(0.65, 1.35) if CLMP else 1.0), int(rng.integers(0, 8)) if var is None else var, tw))
G = np.array([0, 0, -1.0])
def outward_xy(p):
    h = np.array([p[0] - C.x, p[1] - C.y, 0.0]); return h / (np.linalg.norm(h) + 1e-9)

# A) cover layer over the whole shell (strand look, layered edge)
# shingles: shorter locks at all depths whose tips lift off the shell -> layered, jagged surface and silhouette
for k in range(NDEF('n_cover', 72, 200)):
    ap = rng.uniform(-np.pi, np.pi); t0 = rng.uniform(0.02, 0.5 if CLMP else 0.75); t1 = (1.0 if rng.random() < 0.75 else min(1.0, t0 + rng.uniform(0.45, 0.75))) if CLMP else min(1.0, t0 + rng.uniform(0.25, 0.5))   # clumps: long continuous locks to the edge (tips mid-surface read as a pinecone)
    a_root = solve_a(ap, t0); ph = abs(float(phi_of(shell_at(a_root, 1.0)[0][None])[0]))
    ext = rng.uniform(0.004, 0.016) if (t1 >= 1.0 and ph > 45) else 0.0
    def st(s, p, d): return G * 6.0 + outward_xy(p) * 2.0
    add(grow(ap, t0, t1, ext, rng.uniform(0.002, 0.005), st, tiplift=rng.uniform(0.003, F('tiplift', 0.006 if CLMP else 0.012)) * (2.2 if (CLMP and ph > 120) else 1.0), arch=(rng.uniform(0.004, F('arch', 0.010)) if (CLMP and t0 < 0.45 and abs(float(wrap(ap - A_PART))) > 0.35) else 0.0)), rng.uniform(0.014, 0.024))   # top locks swoop up off the part
# B) side locks over the ears: hang to cheekbone / jaw, flared, tips a little inward at the very end
for side in (-1, 1):
    for k in range(NDEF('n_side', 13, 32)):
        ph_t = side * rng.uniform(58, 128)
        # find the swirled azimuth whose edge lies at that world azimuth
        ap = math.radians(ph_t) * 0.98 + rng.normal(0, 0.03)
        t0 = rng.uniform(0.45, 0.8); ext = (0.004 + 0.036 * rng.beta(1.3, 2.2)) * (1.25 if abs(ph_t) < 100 else 1.0)   # mostly short, a few long: layered, no straight cut
        spl = rng.uniform(4.0, 11.0)
        def st(s, p, d, spl=spl): return G * 6.0 + outward_xy(p) * (spl - (8.0 if CLMP else 22.0) * max(0, s - 0.6))   # tips turn down and in toward the jaw   # splayed outline, tips turn down/in at the very end
        add(grow(ap, t0, 1.0, ext, rng.uniform(0.003, 0.008), st, minc=0.006, tiplift=rng.uniform(0.0, 0.008)), rng.uniform(0.013, 0.024))
# B2) flared side locks: stick out sideways at temple / cheek level, then droop (the spiky outline of the reference front view)
for side in (-1, 1):
    for k in range(NDEF('n_flare', 8, 10)):
        ap = math.radians(side * rng.uniform(55, 115)); t0 = rng.uniform(0.55, 0.85); ext = rng.uniform(0.018, 0.04)
        def st(s, p, d): return outward_xy(p) * (12.0 if s < 0.5 else 2.0) + G * (4.0 if s < 0.5 else 12.0)
        add(grow(ap, t0, 1.0, ext, rng.uniform(0.004, 0.008), st, minc=0.007, tiplift=0.006), rng.uniform(0.010, 0.018))
# H) crown cowlick: two broader wisps rising up and back from the top of the crown (side view of the reference)
for k in range(int(kw.get('n_cowlick', 3))):
    ap = rng.uniform(-0.5, 0.5) + np.pi; t0 = rng.uniform(0.03, 0.12)
    def st(s, p, d): return np.array([0, 1.0, -0.4]) * 8.0
    add(grow(ap, t0, t0 + 0.1, rng.uniform(0.02, 0.035), 0.004, st, minc=0.01, tiplift=0.01, cs=0.006), rng.uniform(0.012, 0.018))
# C) nape: short flicked layers
for k in range(NDEF('n_nape', 14, 30)):
    phn = rng.uniform(125, 235); ap = math.radians(phn); t0 = rng.uniform(0.5, 0.85)
    ext = (0.002 + 0.024 * math.cos(math.radians(phn - 180)) ** 4) * rng.uniform(0.5, 1.3) * (0.7 if CLMP else 1.0)   # V at the nape   # longest at the nape centre
    fk = rng.uniform(5.0, 14.0) if CLMP else rng.uniform(2.0, 9.0)
    def st(s, p, d, fk=fk): return G * 5.0 + outward_xy(p) * (1.0 + fk * s)
    add(grow(ap, t0, 1.0, ext, rng.uniform(0.003, 0.007), st, minc=0.006), rng.uniform(0.013, 0.02))
# D) fringe: roots right of the part, swept across the forehead to the character's right, tips about eye level
for k in range(NDEF('n_fringe', 13, 44)):
    # rooted near the part on the front top, leave the shell flow early and walk over it (shell clearance) diagonally across the forehead
    ap = rng.uniform(-0.15, A_PART - 0.03); t0 = rng.uniform(0.55, 0.75); t1 = t0 + 0.05
    ext = rng.uniform(0.07, 0.11); zs = rng.uniform(1.712, 1.745) + 0.03 * max(0.0, ap); lf = rng.uniform(0.003, 0.007)
    tgt = np.array([-rng.uniform(0.35, 0.8), -0.2, -1.0]); tgt /= np.linalg.norm(tgt)   # ~30-45 deg from vertical toward the character's right
    def st(s, p, d, tgt=tgt): return (tgt - d) * F('fr_rate', 45.0)
    _g = grow(ap, t0, t1, ext, lf, st, minc=F('fringe_c', 0.007), cs=lf, raw=True, zstop=zs)
    if kw.get('dbg'): print('FRINGE', round(ap, 2), len(_g), _g[0].round(3), _g[len(_g)//2].round(3), _g[-1].round(3))
    add(_g, rng.uniform(0.02, 0.032), tw=0.62 if CLMP else 0.9)
# E) short locks left of the part framing the other brow
for k in range(NDEF('n_left', 5, 10)):
    ap = rng.uniform(A_PART + 0.05, 0.95); t0 = rng.uniform(0.45, 0.75); ext = rng.uniform(0.010, 0.022)   # short: the character's left eye stays visible
    def st(s, p, d): return G * 9.0 + np.array([0.6, -0.3, 0]) * 4.0
    add(grow(ap, t0, 1.0, ext, rng.uniform(0.003, 0.007), st, minc=0.007), rng.uniform(0.014, 0.022))
# F) crown flicks: thin spikes up and back + a few flyaways
for k in range(int(kw.get('n_spike', 7 if CLMP else 4))):
    ap = rng.uniform(-0.9, 0.9); t0 = rng.uniform(0.12, 0.32)
    base, n = shell_at(solve_a(ap, t0), t0)
    up = np.array(n) * 0.85 + np.array([rng.uniform(-0.3, 0.3), 0.2, 0.35]); up /= np.linalg.norm(up)
    bend = np.array([rng.uniform(-0.02, 0.02), rng.uniform(0.01, 0.025), -0.012])   # wisp arcs back / to the side
    L = rng.uniform(0.03, 0.045); m = 14; pts = [base - n * 0.004 + up * L * (i / m) + bend * (i / m) ** 2 for i in range(m + 1)]
    add(np.array(pts), rng.uniform(0.010, 0.016))
# G) face-framing locks in front of the ears, down to about jaw / mouth level (they break the bowl line at brow and ear height)
for side in (-1, 1):
    for k in range(int(kw.get('n_frame', 4))):
        ap = math.radians(side * rng.uniform(55, 80)); t0 = rng.uniform(0.55, 0.8); zs = rng.uniform(1.618, 1.642)
        def st(s, p, d): return G * 14.0 + outward_xy(p) * 1.5
        add(grow(ap, t0, 1.0, 0.12, 0.005, st, minc=0.007, raw=True, zstop=zs), rng.uniform(0.008, 0.012))
print('CARDS', len(cards))

def build_cards():
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); nseg = 8
    FREE = []; NRM = []   # NRM: both the card and its reversed twin get the card's OUTWARD normal (an inward-lit twin read as black slivers on the crown)
    for pts, w0, var, tw in cards:
        Pp4, L = resample(pts, nseg + 1); Pp = Pp4[:, :3]; FRv = Pp4[:, 3]; OUTS = []
        if L < 0.006: continue
        u0, u1 = var / 8 + 0.004, (var + 1) / 8 - 0.004
        rows = []
        for i in range(nseg + 1):
            s = i / nseg; p = Pp[i]
            tng = Pp[min(i + 1, nseg)] - Pp[max(i - 1, 0)]; tng /= np.linalg.norm(tng) + 1e-12
            loc, nr, _, d = TREE.find_nearest(Vector(p), 0.3); out = np.array(nr) if loc is not None else (p - np.array(C))
            out = out - tng * out.dot(tng); out /= np.linalg.norm(out) + 1e-12
            b = np.cross(tng, out); b /= np.linalg.norm(b) + 1e-12
            w = w0 * (1.0 - 0.25 * s) * tw
            v = 1.0 - s * 0.97
            rows.append([(clearance_push(p - b * w / 2, 0.003), (u0, v)), (clearance_push(p + out * w * 0.14, 0.003), ((u0 + u1) / 2, v)), (clearance_push(p + b * w / 2, 0.003), (u1, v))]); OUTS.append(out)   # card edges too, not only the centre line
        for back in (0, 1):
            vs = []
            for r in rows:
                vrow = []
                for (q, uv) in r:
                    if back:
                        # reversed copy 0.5 mm toward the head
                        loc, nr, _, d = TREE.find_nearest(Vector(q), 0.3); q = q - (np.array(nr) if loc is not None else 0) * 0.0005
                    vrow.append((bm.verts.new(q), uv)); FREE.append(float(FRv[len(vs)])); NRM.append(tuple(OUTS[len(vs)]))
                vs.append(vrow)
            for i in range(nseg):
                for c in range(2):
                    quad = [vs[i][c], vs[i][c + 1], vs[i + 1][c + 1], vs[i + 1][c]]
                    if back: quad = quad[::-1]
                    try: f = bm.faces.new([q[0] for q in quad])
                    except ValueError: continue
                    for l, (_, uv) in zip(f.loops, quad): l[uvl].uv = uv
    bm.normal_update()
    # orient front faces away from the head (back copies the opposite way): count disagreements
    me = bpy.data.meshes.new('HairCards'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('Tobin_Hair', me); bpy.context.scene.collection.objects.link(ob)
    me.normals_split_custom_set_from_vertices(NRM)
    g = ob.vertex_groups.new(name='free')
    for i, w in enumerate(FREE): g.add([i], w, 'REPLACE')
    return ob
def build_clumps():
    """FF / KH style: every lock is a SOLID tapered clump (lens cross-section: domed outside, flatter underside), sharp point, root sunk in the shell.
    UV: around the lens = one lock of the shell texture (u 0..0.25, mirrored so there is no seam; dark gap texels on the lens edges), v along the lock."""
    bm = bmesh.new(); uvl = bm.loops.layers.uv.new('UVMap'); fl = bm.verts.layers.float.new('free'); nseg = int(kw.get('cseg', 11)); NRG = int(kw.get('cring', 12))
    for pts, w0, var, tw in cards:
        Pp4, L = resample(pts, nseg + 1); Pp = Pp4[:, :3]; FRv = Pp4[:, 3]
        if L < 0.008: continue
        u_off = 0.25 * (var % 4)
        rings = []
        for i in range(nseg + 1):
            s = i / nseg; p = Pp[i]
            tng = Pp[min(i + 1, nseg)] - Pp[max(i - 1, 0)]; tng /= np.linalg.norm(tng) + 1e-12
            loc, nr, _, d = TREE.find_nearest(Vector(p), 0.3); out = np.array(nr) if loc is not None else (p - np.array(C))
            out = out - tng * out.dot(tng); out /= np.linalg.norm(out) + 1e-12
            b = np.cross(tng, out); b /= np.linalg.norm(b) + 1e-12
            w = w0 * tw * (1.0 - s ** F('ctaper', 1.6)) * (0.75 + 0.25 * min(1.0, s * 6))   # swell just after the root, taper to a point
            h_out, h_in = w * F('cdome', 0.34), w * 0.08
            ring = []
            for k in range(NRG):
                th = 2 * np.pi * k / NRG; cx, sy = np.cos(th), np.sin(th)
                q = p + b * cx * w / 2 + out * (sy * (h_out if sy > 0 else h_in))
                ring.append((clearance_push(q, 0.003), (u_off + 0.25 * (0.5 + 0.5 * cx) * 0.98 + 0.0025, 1.0 - s * 0.97)))
            rings.append(ring)
        vs = []
        for r in rings:
            row = [bm.verts.new(q) for q, _ in r]
            for v in row: v[fl] = float(FRv[len(vs)])
            vs.append(row)
        for i in range(nseg):
            for k in range(NRG):
                k1 = (k + 1) % NRG
                quad = [vs[i][k], vs[i][k1], vs[i + 1][k1], vs[i + 1][k]]
                uvs = [rings[i][k][1], rings[i][k1][1], rings[i + 1][k1][1], rings[i + 1][k][1]]
                try: f = bm.faces.new(quad)
                except ValueError: continue
                for l, uv in zip(f.loops, uvs): l[uvl].uv = uv
        try: bm.faces.new(vs[0][::-1])   # root cap (inside the shell)
        except ValueError: pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)   # tip rings collapse to a point
    bmesh.ops.dissolve_degenerate(bm, edges=bm.edges, dist=1e-7)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)          # closed tubes: consistent outward winding
    bm.normal_update()
    FREE = [v[fl] for v in bm.verts]
    me = bpy.data.meshes.new('HairClumps'); bm.to_mesh(me); bm.free()
    for p in me.polygons: p.use_smooth = True
    ob = bpy.data.objects.new('Tobin_HairClumps', me); bpy.context.scene.collection.objects.link(ob)
    g = ob.vertex_groups.new(name='free')
    for i, w in enumerate(FREE): g.add([i], w, 'REPLACE')
    return ob
CLUMPS = kw.get('clumps', '1') == '1'
cards_ob = build_clumps() if CLUMPS else build_cards()
# hybrid (FF7R-like): thin alpha flyaway wisps over the solid clumps give the soft, wispy outline of the reference
wisp_ob = None
if CLUMPS and int(kw.get('n_wisp', 1)) > 0:
    cards.clear()
    def wadd(g, w): cards.append((np.asarray(g, float), w * F('wwide', 1.9), int(rng.integers(0, 8)), 1.0))
    for k in range(int(kw.get('n_wtop', 20))):            # rising arcs off the top / part
        ap = rng.uniform(-0.9, 1.3); t0 = rng.uniform(0.15, 0.5); up = rng.uniform(2.0, 5.0)
        def st(s, p, d, up=up): return outward_xy(p) * 4.0 + np.array([0, 0, 1.0]) * up * (1 - s) + G * 6.0 * s
        wadd(grow(ap, t0, t0 + 0.12, rng.uniform(0.05, 0.09), 0.006, st, minc=0.008, cs=0.006), rng.uniform(0.005, 0.010))
    for side in (-1, 1):                                  # side flyaways sticking out of the silhouette
        for k in range(int(kw.get('n_wside', 12))):
            ap = math.radians(side * rng.uniform(55, 125)); t0 = rng.uniform(0.45, 0.8)
            def st(s, p, d): return outward_xy(p) * 9.0 + G * (3.0 + 8.0 * s)
            wadd(grow(ap, t0, 1.0, rng.uniform(0.03, 0.065), 0.006, st, minc=0.008, cs=0.006), rng.uniform(0.004, 0.009))
    for k in range(int(kw.get('n_wfr', 9))):              # fringe strays
        ap = rng.uniform(-0.2, A_PART + 0.3); t0 = rng.uniform(0.5, 0.72); tgt = np.array([-rng.uniform(0.2, 0.9), -0.3, -1.0]); tgt /= np.linalg.norm(tgt)
        def st(s, p, d, tgt=tgt): return (tgt - d) * 40.0
        wadd(grow(ap, t0, t0 + 0.05, rng.uniform(0.06, 0.1), 0.009, st, minc=0.008, cs=0.009, raw=True, zstop=rng.uniform(1.71, 1.75)), rng.uniform(0.004, 0.008))
    for k in range(int(kw.get('n_wnape', 7))):            # nape flicks
        ap = math.radians(rng.uniform(140, 220)); t0 = rng.uniform(0.6, 0.85)
        def st(s, p, d): return G * 4.0 + outward_xy(p) * (2.0 + 9.0 * s)
        wadd(grow(ap, t0, 1.0, rng.uniform(0.015, 0.03), 0.006, st, minc=0.008, cs=0.006), rng.uniform(0.004, 0.008))
    wisp_ob = build_cards()
    print('WISPS', len(cards))
def audit(ob):
    sd = []
    for v in ob.data.vertices:
        loc, nr, _, d = TREE.find_nearest(v.co, 0.3)
        if loc is not None: sd.append((v.co - loc).dot(nr))
    sd = np.array(sd); return int((sd < 0).sum()), int((sd < 0.002).sum()), len(sd), float(sd.min())
print('CLEARANCE shell (inside, <2mm, verts, min m)', audit(shell), 'cards', audit(cards_ob))
_zs = max(v.co.z for v in shell.data.vertices); _zc = max(v.co.z for v in cards_ob.data.vertices); _zh = max(v.co.z for v in bpy.data.objects['AvatarHead'].data.vertices)
print('CROWN skull top %.3f shell +%.1f cm cards +%.1f cm' % (_zh, (_zs - _zh) * 100, (_zc - _zh) * 100))
# ear coverage: rays from the side camera direction onto every ear-region head vertex must hit hair (shell, or shell+cards) first
def _tree_of(ob):
    b = bmesh.new(); b.from_mesh(ob.data); t = BVHTree.FromBMesh(b); b.free(); return t
_ts, _tc = _tree_of(shell), _tree_of(cards_ob); _n = _hs = _ha = 0; _miss = []
for v in bpy.data.objects['AvatarHead'].data.vertices:
    c = v.co
    if abs(c.x) > 0.075 and 1.65 < c.z < 1.73 and -0.09 < c.y < 0.03:
        o = Vector((math.copysign(0.4, c.x), c.y, c.z)); dvec = (c - o); L = dvec.length; dvec.normalize(); _n += 1
        hs = _ts.ray_cast(o, dvec, L - 0.0005)[0] is not None; hc = _tc.ray_cast(o, dvec, L - 0.0005)[0] is not None
        _hs += hs; _ha += hs or hc
        if not (hs or hc): _miss.append((round(c.z, 3), round(c.y, 3)))
print('EARS side rays', _n, 'hit shell', _hs, 'hit shell or cards', _ha, 'miss z', sorted(_miss)[:12])
print('TRIS shell', sum(len(p.vertices) - 2 for p in shell.data.polygons), 'cards', sum(len(p.vertices) - 2 for p in cards_ob.data.polygons))

# ---------------- 5. materials ----------------
def mat(name, prefix, clip):
    m = bpy.data.materials.new(name); m.use_nodes = True; nt = m.node_tree; b = nt.nodes['Principled BSDF']
    def img(n, cs):
        t = nt.nodes.new('ShaderNodeTexImage'); t.image = bpy.data.images.load(f'{TEX}/{prefix}_{n}.png'); t.image.colorspace_settings.name = cs; return t
    c = img('BaseColor', 'sRGB'); nt.links.new(c.outputs['Color'], b.inputs['Base Color'])
    nm = img('Normal', 'Non-Color'); nn = nt.nodes.new('ShaderNodeNormalMap'); nt.links.new(nm.outputs['Color'], nn.inputs['Color']); nt.links.new(nn.outputs['Normal'], b.inputs['Normal'])
    b.inputs['Roughness'].default_value = 0.55; b.inputs['Specular IOR Level'].default_value = 0.35
    if clip:
        g = nt.nodes.new('ShaderNodeMath'); g.operation = 'GREATER_THAN'; g.inputs[1].default_value = 0.38
        nt.links.new(c.outputs['Alpha'], g.inputs[0]); nt.links.new(g.outputs[0], b.inputs['Alpha'])
    m.use_backface_culling = True
    return m
shell.data.materials.append(mat('Tobin_HairShell', 'Hair_Shell', False)); cards_ob.data.materials.append(shell.data.materials[0] if CLUMPS else mat('Tobin_Hair', 'Hair_Cards', True))
if wisp_ob is not None:   # one hair object, two materials: solid clumps (shell material) + alpha wisps (card material)
    wisp_ob.data.materials.append(mat('Tobin_Hair', 'Hair_Cards', True))
    bpy.ops.object.select_all(action='DESELECT'); wisp_ob.select_set(True); cards_ob.select_set(True); bpy.context.view_layer.objects.active = cards_ob
    bpy.ops.object.join()
for o in bpy.data.objects:
    if o.type == 'MESH' and o.name not in ('Tobin_Hair', 'Tobin_HairClumps', 'Tobin_HairShell', 'AvatarHead', 'AvatarEyelashes', 'AvatarLeftEyeball', 'AvatarRightEyeball', 'AvatarTeethLower', 'AvatarTeethUpper', 'AvatarBody'):
        o.hide_render = True
    if o.name == 'Icosphere': o.hide_render = True; o.hide_viewport = True
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT, 'Tobin_shell_hair.blend'))
print('SAVED')

# ---------------- 6. preview renders (EEVEE, reference-like framing on white) ----------------
if PREVIEW:
    if kw.get('cards', '1') == '0': cards_ob.hide_render = True
    if kw.get('shell', '1') == '0': shell.hide_render = True
    if kw.get('views'): VIEWS = kw['views'].split(',')
    else: VIEWS = None
    sc = bpy.context.scene; sc.render.engine = 'BLENDER_EEVEE'; sc.render.resolution_x = sc.render.resolution_y = int(kw.get('px', 800))
    sc.eevee.taa_render_samples = 32; sc.view_settings.view_transform = 'Standard'
    w = bpy.data.worlds.new('W'); sc.world = w; w.use_nodes = True; w.node_tree.nodes['Background'].inputs[0].default_value = (1, 1, 1, 1); w.node_tree.nodes['Background'].inputs[1].default_value = 0.55
    def light(name, typ, rot, e, size=1.0):
        ld = bpy.data.lights.new(name, typ); ld.energy = e
        if typ == 'AREA': ld.size = size
        lo = bpy.data.objects.new(name, ld); sc.collection.objects.link(lo); lo.rotation_euler = [math.radians(x) for x in rot]; return lo
    light('Key', 'SUN', (55, 0, -30), 3.2); light('Fill', 'SUN', (70, 0, 140), 1.0); light('Rim', 'SUN', (60, 0, 200), 1.4)
    camd = bpy.data.cameras.new('Cam'); camd.lens = 85; cam = bpy.data.objects.new('Cam', camd); sc.collection.objects.link(cam); sc.camera = cam
    tgt = Vector((0, -0.04, 1.715)); R = 0.95
    views = {'front': (0, R), 'q34': (-35, R), 'side': (90, R), 'back': (180, R), 'q34b': (140, R), 'close': (-12, 0.55)}
    for nm, (az, rr) in views.items():
        if VIEWS and nm not in VIEWS: continue
        a = math.radians(az); pos = tgt + Vector((math.sin(a) * rr, -math.cos(a) * rr, 0.03))
        cam.location = pos; cam.rotation_euler = (tgt - pos).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(OUT, f'prev_{nm}.png'); bpy.ops.render.render(write_still=True)
    print('PREVIEW_DONE')
