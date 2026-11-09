"""Round 4 SLIDER-FIRST candidate: bounded ridge fit of the template's own (non-eye) head sliders to the photo's landmark positions (mediapipe, eye-aligned, front-ortho)
plus a 3D-target distance term. Landmark model: IDW of the nearest template vertices at the template-hit point of each landmark (linear in the slider coefficients);
residual uses the detector's displacement on the current render (delta formulation: (lm(c)-lm(c_cur)) = (photo - render)). Gauss-Newton on the 3D term.
usage: r4_05_sliders.py <cur_coeffs.json|none> <render_lm.json> <cam.json> <tag> [--ridge 0.5] [--w3 0.3] [--damp 1] [--out coeffs.json]  -> fit4/wrap_r<tag>_{basis,keys,mesh}"""
import json, argparse
from scipy.optimize import lsq_linear
from r4_common import *
from fit_checks import run_checks
import r4_metrics as MT
ap = argparse.ArgumentParser(); [ap.add_argument(a) for a in ('cur', 'lmj', 'camj', 'tag')]
ap.add_argument('--ridge', type=float, default=0.5); ap.add_argument('--w3', type=float, default=0.3); ap.add_argument('--damp', type=float, default=1.0)
A = ap.parse_args()
t = Tpl(); B = t.basis; names = t.slider_names
use = [n for n in names if n.startswith(('Face_', 'Head_', 'Jaw', 'Lips_', 'Nose_'))]
D = np.stack([t.keys[t.slider_idx[names.index(n)]] - B for n in use])        # (K, V, 3)
c_cur = np.zeros(len(use))
if A.cur != 'none': cc = json.load(open(A.cur)); c_cur = np.array([cc.get(n, 0.0) for n in use])
cam = json.load(open(A.camj)); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
rl = json.load(open(A.lmj)); rp = np.array([rl[str(i)] for i in range(468)]); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:468, :2]
px2mm = lambda p: np.stack([cx + (p[:, 0] / R_ - 0.5) * S_, cz + (0.5 - p[:, 1] / R_) * S_], 1)
EC = [33, 263, 133, 362]; s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True); Rr2 = Rr[:2, :2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
hits = json.load(open(D2 + 'fit4/tpl_hits.json')); lid = [i for i in range(468) if str(i) in hits and hits[str(i)] is not None and np.asarray(hits[str(i)]).shape == (3,)]
kd = cKDTree(B); H = np.array([hits[str(i)] for i in lid]); dd, ii = kd.query(H, k=6); ww = 1 / (dd + 1e-3) ** 2; ww /= ww.sum(1, keepdims=True)
dmm = (px2mm(inv(ph[lid])) - px2mm(rp[lid])) * 1e3 * A.damp           # wanted landmark motion (x,z) mm
# landmark jacobian: (L,2,K) mm per unit slider
J = np.stack([(D[:, ii, :][:, :, :, [0, 2]] * ww[None, :, :, None]).sum(2)], 0)[0]   # (K,L,2)
J = np.transpose(J, (1, 2, 0)) * 1e3
Wl = np.ones(len(lid)); big = {n: 1.0 for n in ()}
rows = []; rhs = []
Jl = J.reshape(-1, len(use)); rl_ = dmm.reshape(-1); rows.append(Jl); rhs.append(rl_)
# 3D term (Gauss-Newton, face skin sample)
rng = np.random.default_rng(0); sk = np.where(MT.M['skin'])[0]; sk = rng.choice(sk, min(700, len(sk)), replace=False)
c = c_cur.copy()
for it in range(4):
    P = B + np.tensordot(c, D, 1); dq, q, nq = MT._surf.query(P[sk]); r3 = ((P[sk] - q) * nq).sum(1) * 1e3
    J3 = np.einsum('kvj,vj->vk', D[:, sk, :], nq) * 1e3                              # (n,K) mm per unit slider along the target normal
    # unknown = c (absolute): residual landmarks: Jl (c - c_cur) - rl_ ; 3D: r3 + J3 (c - c_prev)
    Ar = np.vstack([Jl, A.w3 * J3 / np.sqrt(len(sk) / 100.0), A.ridge * np.eye(len(use))])
    br = np.concatenate([rl_ + Jl @ c_cur, A.w3 * (J3 @ c - r3) / np.sqrt(len(sk) / 100.0), np.zeros(len(use))])
    res = lsq_linear(Ar, br, bounds=(0, 1)); c = res.x
print('coeffs:', {n: round(float(v), 2) for n, v in zip(use, c) if v > 0.01})
P = B + np.tensordot(c, D, 1)
pred = Jl @ (c - c_cur); print('landmark residual mm rms: before %.2f after %.2f' % (np.sqrt((rl_ ** 2).mean()), np.sqrt(((rl_ - pred) ** 2).mean())))
def fn(P): tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
flips = int(((fn(P) * fn(B)).sum(1) < 0).sum()); dB = P - B; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = P
np.save(FIT + f'wrap_r{A.tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{A.tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.tag}_mesh.npz', v=P, f=t.faces)
json.dump({n: float(v) for n, v in zip(use, c)}, open(FIT + f'coeffs_{A.tag}.json', 'w'), indent=1)
print('flips', flips, '3D target', MT.tgt_stats(P)); chk = run_checks(t, nk.astype(np.float64), f'sliders {A.tag}', verbose=False); print('GATES PASS=%s' % chk['PASS'])
json.dump(dict(args=vars(A), flips=flips, checks=chk, target=MT.tgt_stats(P)), open(FIT + f'wrap_r{A.tag}_report.json', 'w'), indent=1, default=float)
