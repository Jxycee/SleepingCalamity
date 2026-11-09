# Round 2 step 3-5: non-rigid wrap (nricp_amberg on the WELDED mesh), lips/brow/nose/ears FOLLOW the target, only
# eye_socket + lid band + mouth_interior + nostril interior are pinned during the wrap and harmonically filled after.
# usage: r2_05_wrap.py <round> [--steps 0.05,0.03,0.02,0.01] [--band 4] [--ears data|fill] [--pass2 0.01,0.005,0.002] [--prewarp S]
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import numpy as np, json, sys, trimesh, time, argparse
import scipy.sparse as sp, scipy.sparse.linalg as spl, scipy.sparse.csgraph as cg
from scipy.spatial import cKDTree as KD
from r3_common import *
from fit_checks import run_checks
ap = argparse.ArgumentParser(); ap.add_argument('round'); ap.add_argument('--steps', default='0.05,0.03,0.02,0.01'); ap.add_argument('--band', type=float, default=4.0)
ap.add_argument('--ears', default='data'); ap.add_argument('--pass2', default=''); ap.add_argument('--wl', type=float, default=10.0); ap.add_argument('--wn', type=float, default=0.5)
ap.add_argument('--prewarp', type=float, default=0.0, help='RBF landmark pre-warp smoothing (0=off)'); ap.add_argument('--coll', type=float, default=8.0)
ap.add_argument('--eyer', default='6,16', help='rigid eye zone radii mm (full rigid inside r1, fades to prewarp at r2); 0,0 = off')
ap.add_argument('--eyelm', type=float, default=1.0, help='blend of eye-corner landmark xz shift vs TPS mean (1=landmarks)')
ap.add_argument('--bandlo', type=float, default=45.0, help='neck transition: pins start this many mm below the chin; the band up to the chin is harmonically filled')
ap.add_argument('--intfollow', type=int, default=1); ap.add_argument('--intsmooth', type=int, default=30)
ap.add_argument('--sliders', default='slider_verts.npy'); ap.add_argument('--iters', type=int, default=10)
A = ap.parse_args(); rnd = A.round
t = Tpl(); H = np.load(FIT + 'target_head.npz'); zc = float(np.load(FIT + 'target_aligned_full.npz')['zcut'])
Ps = np.load(FIT + A.sliders).astype(np.float64)
chin_z = t.basis[t.lm_idx[t.lm_names.index('chin_tip')]][2]
# ---- welded mesh
reps = np.unique(t.rep); newid = -np.ones(len(t.rep), int); newid[reps] = np.arange(len(reps)); w_of = newid[t.rep]
Wv = Ps[reps]; wf = w_of[t.faces]; wf = wf[(wf[:, 0] != wf[:, 1]) & (wf[:, 1] != wf[:, 2]) & (wf[:, 0] != wf[:, 2])]
E = edges_of(wf); nW = len(reps)
adj = sp.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(nW, nW)); adj = (adj + adj.T).tocsr()
def W(idx): return np.unique(w_of[np.asarray(idx)])
# ---- core = eye_socket + lid band (band mm around lid rings) + mouth_interior + nostril interior
coreraw = np.zeros(len(t.basis), bool)
for k in ('eye_socket', 'mouth_interior', 'nostril'): coreraw[t.S(k)] = True
for k in ('eyelid_ring_L', 'eyelid_ring_R'):
    coreraw[KD(t.basis[t.S(k)]).query_ball_point(t.basis, A.band * 1e-3, return_length=True) > 0] = True
core = np.zeros(nW, bool); core[w_of[coreraw]] = True
ears = np.zeros(nW, bool); ears[W(t.S('ear_L'))] = True; ears[W(t.S('ear_R'))] = True
notarget = Wv[:, 2] < chin_z - A.bandlo * 1e-3    # round 3: pins start bandlo mm under the chin (round 2: chin-10 mm)
band = (Wv[:, 2] >= chin_z - A.bandlo * 1e-3) & (Wv[:, 2] < chin_z - 0.004) & (np.abs(Wv[:, 0]) < 0.07)
follow = core | band | (ears if A.ears == 'fill' else False)
neck = np.zeros(nW, bool); neck[W(t.S('neck_ring_raw'))] = True
pin = notarget | neck | (core & ~band)
print('welded', nW, 'follow', follow.sum(), 'pinned', pin.sum(), 'core', core.sum())
# ---- target: drop collar bits (below chin level and > coll mm from the slider surface), keep largest component
tv, tfc = H['v'], H['f']
sS = Surf(Ps, t.faces, 400_000); dd, _, _ = sS.query(tv)
dropv = (tv[:, 2] < chin_z + 0.02) & (dd > A.coll * 1e-3)
fk = ~dropv[tfc].any(1); idx = -np.ones(len(tv), int); idx[~dropv] = np.arange((~dropv).sum()); tv2, tf2 = tv[~dropv], idx[tfc[fk]]
_, wid = np.unique(np.round(tv2, 6), axis=0, return_inverse=True); wid = wid.ravel()
e2 = edges_of(wid[tf2]); A2 = sp.coo_matrix((np.ones(len(e2)), (e2[:, 0], e2[:, 1])), shape=(wid.max() + 1,) * 2)
_, lab = cg.connected_components(A2, directed=False); lab = lab[wid]; big = np.argmax(np.bincount(lab)); kv = lab == big
fk2 = kv[tf2].all(1); idx2 = -np.ones(len(tv2), int); idx2[kv] = np.arange(kv.sum()); tv2, tf2 = tv2[kv], idx2[tf2[fk2]]
print('target verts', len(tv), '->', len(tv2), '(collar dropped)')
np.savez(FIT + 'target_head_clean.npz', v=tv2, f=tf2)
surf = Surf(tv2, tf2, 1_200_000)
sv, sf = trimesh.remesh.subdivide(tv2, tf2); tgt = trimesh.Trimesh(sv, sf, process=False)
def nricp(srcv, steps_list):
    src = trimesh.Trimesh(srcv.copy(), wf, process=False)
    steps = [[s, A.wl, A.wn, A.iters] for s in steps_list]
    pidx = np.where(pin)[0]
    return trimesh.registration.nricp_amberg(src, tgt, source_landmarks=pidx, target_positions=srcv[pidx].copy(), steps=steps, eps=1e-5,
                                             distance_threshold=0.02 / src.scale, use_faces=False, use_vertex_normals=True)
t0 = time.time(); cur = Wv.copy()
if A.prewarp > 0:
    from scipy.interpolate import RBFInterpolator
    pw = np.load(FIT + 'tpl_to_tgt_landmarks.npz'); ps, pt = pw['slider_pts'], pw['target_pts']
    rb = RBFInterpolator(ps * 1e3, (pt - ps) * 1e3, kernel="thin_plate_spline", smoothing=A.prewarp)
    dw = rb(cur * 1e3) * 1e-3
    zf = np.clip((cur[:, 2] - (chin_z - A.bandlo * 1e-3)) / (A.bandlo * 1e-3 + 0.005), 0, 1); dw *= (zf * zf * (3 - 2 * zf))[:, None]; dw[notarget | neck] = 0   # fade the prewarp out towards the pinned neck/crop boundary
    r1, r2 = [float(x) * 1e-3 for x in A.eyer.split(',')]
    if r2 > 0:  # keep each eye (lids + socket) rigid under the prewarp: translate by the mean ring displacement, fade into the TPS field
        for side in ('L', 'R'):
            ring = np.unique(w_of[t.S('eyelid_ring_' + side)]); m = dw[ring].mean(0)
            if A.eyelm: m[[0, 2]] = A.eyelm * np.load(FIT + 'eye_shift.npz')[side][[0, 2]] + (1 - A.eyelm) * m[[0, 2]]   # x,z from the eye-corner landmarks
            dist = KD(cur[ring]).query(cur)[0]; x = np.clip((dist - r1) / (r2 - r1), 0, 1); wgt = 1 - x * x * (3 - 2 * x)
            sidemask = (cur[:, 0] > 0) == (side == 'L')   # character-left = +X
            wgt = wgt * sidemask
            dw = wgt[:, None] * m + (1 - wgt[:, None]) * dw
            print('eye', side, 'rigid shift mm', np.round(m * 1e3, 2))
    cur = cur + dw
    print('prewarp: mean |disp| mm', np.linalg.norm(dw, axis=1).mean() * 1e3)
pre = cur.copy()
wrap = cur.copy() if A.steps == 'none' else nricp(cur, [float(x) for x in A.steps.split(',')])
if A.pass2: wrap = nricp(wrap, [float(x) for x in A.pass2.split(',')])
print('nricp s', round(time.time() - t0))
d = wrap - pre   # residual after the smooth prewarp; core gets harmonic fill of this residual and keeps the prewarp displacement
# ---- harmonic fill of the displacement on the follow set (uniform Laplacian, Dirichlet from the rest)
fi = np.where(follow)[0]; bi = np.where(~follow)[0]
L = sp.diags(np.asarray(adj.sum(1)).ravel()) - adj; Lff = L[fi][:, fi].tocsc(); Lfb = L[fi][:, bi]
dn = d.copy()
for j in range(3): dn[fi, j] = spl.spsolve(Lff, -Lfb @ d[bi, j])
final = pre + dn; final[notarget | neck] = Wv[notarget | neck]
# round 3: nostril + mouth-interior vertices follow the outer skin around them (IDW of the displacement of the nearest skin vertices) so they cannot poke through the wing / lip corner
if A.intfollow:
    inner = np.zeros(nW, bool); inner[W(t.S('nostril'))] = True; inner[W(t.S('mouth_interior'))] = True
    _lp = np.concatenate([t.S('lip_patch_upper_verts'), t.S('lip_patch_lower_verts')]); _keepedge = np.zeros(nW, bool)
    for _k in ('lip_line', 'lip_seam_all', 'lip_seam_front'): _keepedge[W(t.S(_k))] = True
    _lpw = np.zeros(nW, bool); _lpw[W(_lp)] = True; inner |= _lpw & ~_keepedge      # inner lip surface follows the outer skin too (nricp snapped it onto the cheek skin: front-visible pink patches)
    outer = ~(core | pin | band | inner | ears) & (Wv[:, 2] > chin_z)
    kd = KD(Wv[outer]); dd_, ii_ = kd.query(Wv[inner], k=8); ww_ = 1.0 / (dd_ + 1e-3) ** 2; ww_ /= ww_.sum(1, keepdims=True)
    disp = final - Wv; oi = np.where(outer)[0]
    before = np.linalg.norm(disp[inner], axis=1).mean() * 1e3
    final[inner] = Wv[inner] + (disp[oi][ii_] * ww_[..., None]).sum(1)
    # Jacobi smoothing of the inner displacement (neighbours that are not inner stay fixed): removes the thin-face flips at the lip rim
    di = final - Wv; ii = np.where(inner)[0]; dg = np.asarray(adj.sum(1)).ravel()
    for _ in range(A.intsmooth):
        nb = (adj @ di) / np.maximum(dg, 1)[:, None]; di[ii] = 0.5 * di[ii] + 0.5 * nb[ii]
    final[inner] = Wv[inner] + di[inner]
    print('inner follow: %d verts, mean |disp| mm %.2f (was %.2f)' % (inner.sum(), np.linalg.norm(final[inner] - Wv[inner], axis=1).mean() * 1e3, before))
newP = final[w_of]; assert np.abs(newP[t.rep] - newP).max() < 1e-12
from fit_common import repair_flips
newP, _f0, _f1 = repair_flips(t, newP); print('wrap flips before/after repair', _f0, _f1)   # generic: local displacement smoothing around any flipped face
# ---- metrics
face = (t.rw == 1.0) & (t.basis[:, 2] > zc + 0.01)
exc = coreraw.copy()
for k in ('lip_line', 'lip_seam_all', 'lip_seam_back', 'eyelid_ring_L', 'eyelid_ring_R'):
    exc[KD(t.basis[t.S(k)]).query_ball_point(t.basis, 0.008, return_length=True) > 0] = True
skin = face & ~exc
def near(name, r): return np.linalg.norm(t.basis - t.basis[t.lm_idx[t.lm_names.index(name)]], axis=1) < r
regions = {'nose': near('nose_tip', 0.022) & face, 'brow': (near('brow_center', 0.02) | near('glabella', 0.02)) & face,
           'lips': (KD(t.basis[t.S('lip_line')]).query_ball_point(t.basis, 0.012, return_length=True) > 0) & face}
def st(P):
    out = {}
    for nme, m in (('face', face), ('skin', skin), ('nose', regions['nose']), ('brow', regions['brow']), ('lips', regions['lips'])):
        a, b = p2s_stats(P, surf, m); out[nme + '_rms'] = a; out[nme + '_p95'] = b
    return out
stats = {'basis': st(t.basis), 'sliders': st(Ps), 'wrap': st(newP)}
for k, v in stats.items(): print(k, {a: round(b, 2) for a, b in v.items()})
def fn(P): tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
flips = int(((fn(newP) * fn(t.basis)).sum(1) < 0).sum()); print('whole-head rest flips vs template', flips)
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
tag = f'r{rnd}'
np.save(FIT + f'wrap_{tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_{tag}_keys.npy', nk)
np.savez(FIT + f'wrap_{tag}_mesh.npz', v=newP, f=t.faces)
chk = run_checks(t, nk.astype(np.float64), f'round {rnd} {vars(A)}')
json.dump(dict(round=rnd, args=vars(A), stats=stats, flips=flips, checks=chk), open(FIT + f'wrap_{tag}_report.json', 'w'), indent=1, default=float)
