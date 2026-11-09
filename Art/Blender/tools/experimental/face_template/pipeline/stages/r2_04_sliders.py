# Round 2 step 2: bounded ridge LSQ over face slider keys, point-to-plane, alternating correspondences.
# Differences from round 1: lip lines stay IN the fit; data zeroed only within ZR mm of the eyelid rings (not 8 mm) + socket/mouth-interior/nostril interiors.
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import numpy as np, json, sys
from scipy.optimize import lsq_linear
from scipy.spatial import cKDTree as KD
from r2_common import *
lam = float(sys.argv[1]) if len(sys.argv) > 1 else 1e-3
ZR = float(sys.argv[2]) * 1e-3 if len(sys.argv) > 2 else 0.004
NOEYE = len(sys.argv) > 3 and sys.argv[3] == 'noeye'; OUT = sys.argv[4] if len(sys.argv) > 4 else ''
t = Tpl(); H = np.load(FIT + 'target_head.npz'); zc = float(np.load(FIT + 'target_aligned_full.npz')['zcut'])
surf = Surf(H['v'], H['f'], 1_200_000)
excl = {'General+', 'General-', 'Neck+', 'Neck-', 'Shoulders+', 'Shoulders-', 'Eye_Blink_L', 'Eye_Blink_R', 'Eye_Wink_L', 'Eye_Wink_R', 'Eye_Wink_HL', 'Eye_Wink_HR'}
if NOEYE: excl |= {n for n in t.slider_names if n.startswith(('Eye', 'EyeB', 'Eyelid'))}
ks = [(n, int(i)) for n, i in zip(t.slider_names, t.slider_idx) if n not in excl]
Dk = np.stack([t.keys[i] - t.basis for _, i in ks]); K = len(ks); N = len(t.basis)
zero = np.zeros(N, bool)
for k in ('eye_socket', 'mouth_interior', 'nostril'): zero[t.S(k)] = True
for k in ('eyelid_ring_L', 'eyelid_ring_R'):
    zero[KD(t.basis[t.S(k)]).query_ball_point(t.basis, ZR, return_length=True) > 0] = True
cand = (t.rw > 0) & ~zero & (t.basis[:, 2] > zc + 0.01)
face = (t.rw == 1.0) & (t.basis[:, 2] > zc + 0.01)
# 'skin only' = face minus socket/mouth-interior/nostril and an 8 mm lid and lip band (same definition as round 1 plus lips, comparable to r1 fitset)
core = np.zeros(N, bool)
for k in ('eye_socket', 'mouth_interior', 'nostril'): core[t.S(k)] = True
for k in ('eyelid_ring_L', 'eyelid_ring_R', 'lip_line', 'lip_seam_all', 'lip_seam_back'):
    core[KD(t.basis[t.S(k)]).query_ball_point(t.basis, 0.008, return_length=True) > 0] = True
skin = face & ~core
def stats(P):
    d, _, _ = surf.query(P[face]); d2, _, _ = surf.query(P[skin]); d3, _, _ = surf.query(P[cand & face])
    return dict(face_rms=float(np.sqrt((d**2).mean())*1e3), face_p95=float(np.percentile(d,95)*1e3), skin_rms=float(np.sqrt((d2**2).mean())*1e3), skin_p95=float(np.percentile(d2,95)*1e3),
                fitset_rms=float(np.sqrt((d3**2).mean())*1e3))
print('basis', stats(t.basis))
c = np.zeros(K); P = t.basis.copy()
for it in range(12):
    d, q, nt = surf.query(P); npl = vertex_normals(P, t.faces)
    ok = cand & (d < 0.02) & ((npl * nt).sum(1) > 0.3)
    w = np.sqrt(t.rw * ok)[ok]; idx = np.where(ok)[0]
    A = np.einsum('knj,nj->nk', Dk[:, idx], nt[idx]) * w[:, None]; b = ((q[idx] - t.basis[idx]) * nt[idx]).sum(1) * w
    r = lsq_linear(np.vstack([A, np.sqrt(lam) * np.eye(K)]), np.concatenate([b, np.zeros(K)]), bounds=(0, 1), method='bvls')
    c = r.x; P = t.basis + np.einsum('k,knj->nj', c, Dk)
    s = stats(P); print(it, 'corr', ok.sum(), {k: round(v, 3) for k, v in s.items()})
res = dict(lam=lam, zr_mm=ZR * 1e3, coeffs={n: float(c[i]) for i, (n, _) in enumerate(ks)}, stats=stats(P), basis_stats=stats(t.basis))
json.dump(res, open(FIT + f'slider_coeffs{OUT}.json', 'w'), indent=1); np.save(FIT + f'slider_verts{OUT}.npy', P.astype(np.float32))
print('sliders moved (>0.02):', {k: round(v, 2) for k, v in sorted(res['coeffs'].items(), key=lambda x: -x[1]) if v > 0.02})
