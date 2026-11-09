"""Round 4 CONSTRAINED correction (replaces r3_03_corr). Differences vs round 3:
 - landmark pairs only (eyes excluded as before; brow, nose, mouth, chin; NO jaw-side/face-side pairs, NO silhouette pairs)
 - thin-plate field in (x,z), strong smoothing, field faded to 0 beyond --reach mm of the face region, per-vertex displacement cap (--cap mm outside lips/brow, --capl inside)
 - depth from the Hunyuan 3D target: after the (x,z) field every skin vertex is projected back onto the target surface along the target normal (--snap blend),
   so features slide ALONG the target surface (distance to the target stays small) and y is never invented
 - eyes rigid (ring translation), pins fade under the chin, delta-transfer of all keys, gates
usage: r4_03_corr.py <in_basis.npy> <render_lm.json> <cam.json> <render.png> <tag> [--smooth 100] [--damp 1] [--cap 4] [--capl 8] [--snap 1] [--ex ids] [--only ids]"""
import json, argparse
from scipy.interpolate import RBFInterpolator
from r4_common import *
from fit_checks import run_checks
import r4_metrics as MT
ap = argparse.ArgumentParser(); ap.add_argument('inb'); ap.add_argument('lmj'); ap.add_argument('camj'); ap.add_argument('png'); ap.add_argument('tag')
ap.add_argument('--smooth', type=float, default=100.0); ap.add_argument('--damp', type=float, default=1.0); ap.add_argument('--cap', type=float, default=4.0); ap.add_argument('--capl', type=float, default=8.0)
ap.add_argument('--snap', type=float, default=1.0); ap.add_argument('--snapfree', type=int, default=1, help='1 = no snap on nose/lips/brow regions (their landmark moves are the point)'); ap.add_argument('--snapsmooth', type=int, default=15); ap.add_argument('--snapit', type=int, default=3); ap.add_argument('--snapmode', default='y', help='y = move only y onto the target (ray along the view axis, where the target faces the camera), n = along the target normal'); ap.add_argument('--reach', type=float, default=30.0); ap.add_argument('--fadelo', type=float, default=45.0)
ap.add_argument('--ex', default=''); ap.add_argument('--only', default=''); ap.add_argument('--ring', type=int, default=1, help='eye-ring landmarks join the pairs as anchors'); ap.add_argument('--nobrow', type=int, default=0); ap.add_argument('--eyer', default='6,16')
ap.add_argument('--pairmax', type=float, default=12.0, help='drop landmark pairs farther apart than this (mm): detector outliers')
A = ap.parse_args()
t = Tpl(); P0 = np.load(A.inb).astype(np.float64); cam = json.load(open(A.camj)); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
chin_z = t.basis[t.lm_idx[t.lm_names.index('chin_tip')]][2]
rl = json.load(open(A.lmj)); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]
rp = np.array([rl[str(i)] for i in range(468)])
px2mm = lambda p: np.stack([cx + (p[:, 0] / R_ - 0.5) * S_, cz + (0.5 - p[:, 1] / R_) * S_], 1)
EC = [33, 263, 133, 362]
s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True)
Rr2 = Rr[:2, :2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
RING = [33,7,163,144,145,153,154,155,133,173,157,158,159,160,161,246, 362,382,381,380,374,373,390,249,263,466,388,387,386,385,384,398]
MOUTH = [0,267,269,270,409,291,375,321,405,314,17,84,181,91,146,61,185,40,39,37,78,191,80,81,82,13,312,311,310,415,308,324,318,402,317,14,87,178,88,95]
BROW = [70,63,105,66,107,55,65,52,53,46,300,293,334,296,336,285,295,282,283,276]
NOSE = [1,2,4,5,6,19,94,98,327,129,358,168,195,197,48,278,64,294,97,326]
CHIN = [152,148,176,149,150,377,400,378,379]
use = MOUTH + NOSE + CHIN + RING + ([] if A.nobrow else BROW)
if A.only: use = [int(x) for x in A.only.split(',') if x]
ap2 = None
excl = {int(x) for x in A.ex.split(',') if x}
if not A.ring: excl |= set(RING)
ids = [i for i in use if i not in excl]
src_mm = px2mm(rp[ids]); tgt_mm = px2mm(inv(ph[ids])); d = tgt_mm - src_mm; mag = np.linalg.norm(d, axis=1) * 1e3
ok = mag < A.pairmax; print('landmark pairs', ok.sum(), 'of', len(ids), 'median |d| mm %.2f p95 %.2f max %.2f' % (np.median(mag), np.percentile(mag, 95), mag.max()))
Su = src_mm[ok] * 1e3; Tu = tgt_mm[ok] * 1e3
rb = RBFInterpolator(Su, Tu - Su, kernel='thin_plate_spline', smoothing=A.smooth, degree=1)
dxz = rb(P0[:, [0, 2]] * 1e3) * 1e-3 * A.damp
ft = np.clip((P0[:, 2] - (chin_z - A.fadelo * 1e-3)) / (A.fadelo * 1e-3 + 0.005), 0, 1); ft = ft * ft * (3 - 2 * ft)
# reach: fade to 0 beyond A.reach mm of the face-skin region (skull, ears, back stay as they are)
fk = cKDTree(t.basis[MT.M['face']]); dist = fk.query(P0)[0] * 1e3; fr = np.clip(1 - (dist - 5) / max(A.reach, 1e-6), 0, 1); fr = fr * fr * (3 - 2 * fr)
dw = np.zeros_like(P0); dw[:, 0] = dxz[:, 0] * ft * fr; dw[:, 2] = dxz[:, 1] * ft * fr
EY1, EY2 = [float(v) * 1e-3 for v in A.eyer.split(',')]
for side in ('L', 'R'):
    ring = t.S('eyelid_ring_' + side).astype(int); m = dw[ring].mean(0)
    dd = cKDTree(P0[ring]).query(P0)[0]; x = np.clip((dd - EY1) / (EY2 - EY1), 0, 1); wgt = 1 - x * x * (3 - 2 * x)
    wgt = wgt * ((P0[:, 0] > 0) == (side == 'L')); dw = wgt[:, None] * m + (1 - wgt[:, None]) * dw
# per-vertex cap (soft): lips/brow region may move more
lb = np.zeros(len(P0), bool); lb |= MT.M['lips'] | MT.M['brow'] | MT.M['nose']; lbw = np.clip(1 - cKDTree(P0[lb]).query(P0)[0] * 1e3 / 6.0, 0, 1)
cap = (A.cap + (A.capl - A.cap) * lbw) * 1e-3; nrm_ = np.linalg.norm(dw, axis=1) + 1e-12; dw *= np.minimum(1.0, cap / nrm_)[:, None]
newP = P0 + dw
# ---- depth from the 3D target: project skin vertices back to the target surface along the target normal (graph-smoothed weight)
reps = np.unique(t.rep); nid = -np.ones(len(t.rep), int); nid[reps] = np.arange(len(reps)); wof = nid[t.rep]
wf = wof[t.faces]; wf = wf[(wf[:, 0] != wf[:, 1]) & (wf[:, 1] != wf[:, 2]) & (wf[:, 0] != wf[:, 2])]
E = edges_of(wf); nW = len(reps); adj = sp.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(nW, nW)); adj = (adj + adj.T).tocsr(); deg = np.asarray(adj.sum(1)).ravel()
sm_ = MT.M['skin'].copy()
if A.snapfree: sm_ &= ~(MT.M['nose'] | MT.M['lips'] | MT.M['brow'])
wm = np.zeros(nW); wm[wof[sm_]] = 1.0
for _ in range(10): wm = 0.5 * wm + 0.5 * (adj @ wm) / np.maximum(deg, 1)
ws = wm[wof] * A.snap
dq, q, nq = MT._surf.query(newP); sd = ((newP - q) * nq).sum(1)
if A.snapmode == 'y':
    ny = nq[:, 1]; dy = np.where((np.abs(ny) > 0.45) & (np.abs(sd) < 0.008), -sd / np.where(np.abs(ny) > 0.45, ny, 1.0), 0.0); dy = np.clip(dy, -0.004, 0.004) * ws
    newP[:, 1] += dy; mv = dy
else:
    base = newP.copy(); cur = newP.copy()
    for _it in range(A.snapit):
        dq, q, nq = MT._surf.query(cur); sd = ((cur - q) * nq).sum(1)
        mv = np.where(np.abs(sd) < 0.010, sd, 0.0) * ws; cur = cur - mv[:, None] * nq
    sdisp = cur - base                      # snap displacement field; smooth it on the welded graph so target noise does not bump the skin
    wd = np.zeros((nW, 3)); wd[wof] = sdisp
    for _ in range(A.snapsmooth): wd = 0.4 * wd + 0.6 * (adj @ wd) / np.maximum(deg, 1)[:, None]
    newP = base + wd[wof]; mv = np.linalg.norm(newP - base, axis=1)
print('field |d| mm: mean %.2f max %.2f ; snap |d| mean %.3f max %.3f mm (on %d verts)' % (np.linalg.norm(dw, axis=1).mean() * 1e3, np.linalg.norm(dw, axis=1).max() * 1e3, np.abs(mv).mean() * 1e3, np.abs(mv).max() * 1e3, (ws > .5).sum()))
def fn(P): tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
flips = int(((fn(newP) * fn(t.basis)).sum(1) < 0).sum()); print('whole-head rest flips vs template', flips)
assert np.abs(newP[t.rep] - newP).max() < 1e-4, 'weld seams split'
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{A.tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{A.tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.tag}_mesh.npz', v=newP, f=t.faces)
st = MT.tgt_stats(newP); print('3D target', st)
chk = run_checks(t, nk.astype(np.float64), f'corr {A.tag}', verbose=False); print('GATES PASS=%s' % chk['PASS'])
for k, v in chk.items():
    if isinstance(v, dict): print(' ', k, json.dumps({a: (round(float(b), 3) if not isinstance(b, dict) else {x: round(float(y), 2) for x, y in b.items()}) for a, b in v.items()}))
json.dump(dict(args=vars(A), flips=flips, target=st, checks=chk), open(FIT + f'wrap_r{A.tag}_report.json', 'w'), indent=1, default=float)
