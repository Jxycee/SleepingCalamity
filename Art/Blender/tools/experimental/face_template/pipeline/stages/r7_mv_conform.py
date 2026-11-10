"""Multi-view mode: conform the NON-face parts of the wrap (skull, back of the head, ears, neck sides) to the 3D target.
Why: the single-photo wrap (r3_02) leaves a flat planar cut behind the crown and narrow ears (measured on mv1: side joint distance 4.8 vs 2.1 for the TRELLIS shape, up to 15 mm at the occiput),
and the photo-ratio stages after it (r4_02 global skull stretch, r6_06 slider/warp fit, skullw, polish, skulldepth) were designed to fix a single photo's missing depth: for a trusted 3D shape they only move the head away from it.
Method (replaces r4_02 in multiview mode): iterate (default 10): every free vertex moves ALONG THE TARGET NORMAL toward its nearest target surface point (signed distance, alpha per step, only where the
target is closer than --maxmm: no jumps across the head), the displacement field is Jacobi-smoothed on the welded vertex graph (--smooth rings: no bumps, no scratches), weights fade to 0 on the face region
(eyes, lids, lips, nose, brow stay exactly as the wrap and the later face snap made them, blink/viseme rows keep their gates) over --fade mm and to 0 on the pinned lower neck; flips are damped locally (deflip).
Keys are delta-transferred (nk = keys + (P - basis)) exactly like r6_03; gates (fit_checks6) are reported.
usage: r7_mv_conform.py <in_tag> <out_tag> [--iters 10] [--alpha 0.6] [--maxmm 26] [--fade 12] [--smooth 8] [--earw 1.0]"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json, argparse
from r6_common import *
from fit_checks6 import run_checks
from fit_common import repair_flips
import r6_metrics as MT
ap = argparse.ArgumentParser(); ap.add_argument('intag'); ap.add_argument('outtag'); ap.add_argument('--iters', type=int, default=10); ap.add_argument('--alpha', type=float, default=0.6)
ap.add_argument('--maxmm', type=float, default=26.0); ap.add_argument('--fade', type=float, default=12.0); ap.add_argument('--smooth', type=int, default=8); ap.add_argument('--earw', type=float, default=1.0)
A = ap.parse_args()
t = Tpl(); P0 = np.load(FIT + f'wrap_r{A.intag}_basis.npy').astype(np.float64); surf = MT._surf
chin_z = t.basis[t.lm_idx[t.lm_names.index('chin_tip')]][2]
# welded graph
reps = np.unique(t.rep); nid = -np.ones(len(t.rep), int); nid[reps] = np.arange(len(reps)); wof = nid[t.rep]
wf = wof[t.faces]; wf = wf[(wf[:, 0] != wf[:, 1]) & (wf[:, 1] != wf[:, 2]) & (wf[:, 0] != wf[:, 2])]
E = edges_of(wf); nW = len(reps); adj = sp.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(nW, nW)); adj = (adj + adj.T).tocsr(); deg = np.maximum(np.asarray(adj.sum(1)).ravel(), 1)
# weights: 1 on skull/ears/neck sides, 0 on the face region (+ fade), 0 on the pinned lower neck
face = MT.M['face'].copy()
for k in ('eye_socket', 'mouth_interior', 'nostril', 'lip_line', 'lip_seam_all', 'eyelid_ring_L', 'eyelid_ring_R'): face[t.S(k)] = True
dface = cKDTree(t.basis[face]).query(t.basis)[0] * 1e3; w = np.clip((dface - 1.0) / max(A.fade, 1e-6), 0, 1); w = w * w * (3 - 2 * w)
zf = np.clip((P0[:, 2] - (chin_z - 0.05)) / 0.04, 0, 1); w = w * zf * zf * (3 - 2 * zf)
ear = np.zeros(len(w), bool); ear[t.S('ear_L')] = True; ear[t.S('ear_R')] = True; ear = np.isin(t.rep, t.rep[ear]); w = np.where(ear, np.minimum(1.0, A.earw) * np.maximum(w, 0.0), w)
wd_ = np.zeros(nW); np.maximum.at(wd_, wof, w); w = wd_[wof]
def weld_mean(X):
    S_ = np.zeros((nW, 3)); C = np.zeros(nW); np.add.at(S_, wof, X); np.add.at(C, wof, 1.0); return S_ / C[:, None]
def fn_(P):
    tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
FN0 = fn_(t.basis)
def margin_flips(P):   # flips with a margin: float32 rounding downstream (basis.npy) flips near-degenerate faces that float64 still calls fine; count cos < 0 against the template normal after the cast
    return int(((fn_(P.astype(np.float32).astype(np.float64)) * FN0).sum(1) < 0.0).sum())
cur = P0.copy(); hist = []; best = (margin_flips(P0), P0.copy())
for it in range(A.iters):
    d, q, nq = surf.query(cur); sd = ((q - cur) * nq).sum(1)
    ok = (d < A.maxmm * 1e-3).astype(float)
    disp = (A.alpha * sd * ok * w)[:, None] * nq
    W3 = weld_mean(disp)
    for _ in range(A.smooth): W3 = 0.5 * W3 + 0.5 * (adj @ W3) / deg[:, None]
    new = cur + W3[wof] * (w > 0)[:, None]; new = deflip(t, cur, new); step = np.linalg.norm(new - cur, axis=1) * 1e3
    for _try in range(8):   # freeze the vertices of any face that would flip (float32-safe) at their previous position, for good, and carry on with the rest
        bad = ((fn_(new.astype(np.float32).astype(np.float64)) * FN0).sum(1) < 0.0)
        if not bad.any(): break
        vs = np.unique(t.rep[t.faces[bad]]); msk = np.isin(t.rep, vs); w[msk] = 0.0; new[msk] = cur[msk]; nfrozen = int((w == 0).sum())
    nfl = margin_flips(new)
    if nfl > 0: print('it %d: flips %d > best %d, stop (late tiny steps flip near-degenerate faces)' % (it, nfl, best[0])); break
    best = (nfl, new.copy()); cur = new; free = w > 0.5; df = surf.query(cur[free])[0] * 1e3; hist.append(dict(it=it, step_mean_mm=float(step[free].mean()), step_max_mm=float(step.max()), free_rms_mm=float(np.sqrt((df ** 2).mean())), flips=nfl))
    print('it %d step mean %.2f max %.2f mm; free-vertex distance to target rms %.2f mm, flips %d' % (it, hist[-1]['step_mean_mm'], hist[-1]['step_max_mm'], hist[-1]['free_rms_mm'], nfl), flush=True)
cur = best[1]; assert np.abs(cur[t.rep] - cur).max() < 1e-4, 'weld seams split'
newP = cur.astype(np.float32).astype(np.float64); f0 = f1 = int(face_flips(t, newP).sum()); print('flips (after float32 rounding, as saved)', f1)   # repair_flips made 7 near-degenerate ear faces worse (9): the per-iteration freeze above is the flip control
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{A.outtag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{A.outtag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.outtag}_mesh.npz', v=newP, f=t.faces)
st = MT.tgt_stats(newP); print('3D target', st)
chk = run_checks(t, nk.astype(np.float64), f'conform {A.outtag}', verbose=False); print('GATES PASS=%s' % chk['PASS'])
moved = np.linalg.norm(newP - P0, axis=1) * 1e3; print('moved from wrap: free mean %.2f max %.2f mm' % (moved[w > 0.5].mean(), moved.max()))
json.dump(dict(args=vars(A), hist=hist, flips=int(f1), target=st, checks=chk, moved_mm_free_mean=float(moved[w > 0.5].mean()), moved_mm_max=float(moved.max())), open(FIT + f'wrap_r{A.outtag}_report.json', 'w'), indent=1, default=float)
print('CONFORM DONE')
