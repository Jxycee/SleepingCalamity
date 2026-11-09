# Step 3: bounded ridge LSQ over the 77 face slider keys, point-to-plane, alternating correspondences.
import numpy as np, json, sys
from scipy.optimize import lsq_linear
from fit_common import *
lam = float(sys.argv[1]) if len(sys.argv) > 1 else 1e-3
t = Tpl(); H = np.load(FIT + 'target_head.npz'); zc = float(np.load(FIT + 'target_aligned_full.npz')['zcut'])
surf = Surf(H['v'], H['f'], 1_200_000)
excl = {'General+', 'General-', 'Neck+', 'Neck-', 'Shoulders+', 'Shoulders-', 'Eye_Blink_L', 'Eye_Blink_R', 'Eye_Wink_L', 'Eye_Wink_R', 'Eye_Wink_HL', 'Eye_Wink_HR'}  # blink/wink are expressions, not shape
ks = [(n, int(i)) for n, i in zip(t.slider_names, t.slider_idx) if n not in excl]
Dk = np.stack([t.keys[i] - t.basis for _, i in ks])  # K,N,3
K = len(ks); N = len(t.basis)
zero = np.zeros(N, bool)
for k in ('eye_socket', 'mouth_interior', 'nostril'): zero[t.S(k)] = True
from scipy.spatial import cKDTree as _KD
for k in ('eyelid_ring_L', 'eyelid_ring_R', 'lip_line', 'lip_seam_all', 'lip_seam_back'):  # no data on lid/lip bands (target lids are blank bumps, lips parted)
    zero[_KD(t.basis[t.S(k)]).query_ball_point(t.basis, 0.008, return_length=True) > 0] = True
cand = (t.rw > 0) & ~zero & (t.basis[:, 2] > zc + 0.01)
face = (t.rw == 1.0) & (t.basis[:, 2] > zc + 0.01)
fitset = face & ~zero
def stats(P):
    d, _, _ = surf.query(P[face]); d2, _, _ = surf.query(P[fitset])
    return dict(face_rms=float(np.sqrt((d**2).mean())*1e3), face_p95=float(np.percentile(d,95)*1e3), fit_rms=float(np.sqrt((d2**2).mean())*1e3), fit_p95=float(np.percentile(d2,95)*1e3))
print('basis', stats(t.basis))
from fit_common import vertex_normals
c = np.zeros(K); P = t.basis.copy()
for it in range(12):
    d, q, nt = surf.query(P)
    npl = vertex_normals(P, t.faces)
    ok = cand & (d < 0.02) & ((npl * nt).sum(1) > 0.3)
    w = np.sqrt(t.rw * ok)[ok]
    idx = np.where(ok)[0]
    A = np.einsum('knj,nj->nk', Dk[:, idx], nt[idx]) * w[:, None]
    b = ((q[idx] - t.basis[idx]) * nt[idx]).sum(1) * w
    A2 = np.vstack([A, np.sqrt(lam) * np.eye(K)]); b2 = np.concatenate([b, np.zeros(K)])
    r = lsq_linear(A2, b2, bounds=(0, 1), method='bvls')
    c = r.x; P = t.basis + np.einsum('k,knj->nj', c, Dk)
    s = stats(P); print(it, 'corr', ok.sum(), {k: round(v, 3) for k, v in s.items()})
res = dict(lam=lam, coeffs={n: float(c[i]) for i, (n, _) in enumerate(ks)}, stats=stats(P), basis_stats=stats(t.basis))
json.dump(res, open(FIT + f'slider_coeffs_lam{lam:g}.json', 'w'), indent=1)
np.save(FIT + f'slider_verts_lam{lam:g}.npy', P.astype(np.float32))
print('top sliders:', sorted(res['coeffs'].items(), key=lambda x: -x[1])[:15])
