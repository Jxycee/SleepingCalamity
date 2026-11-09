# Step 1+2: landmark Umeyama + trimmed scaled ICP on face skin; crop target to head; save aligned target.
import numpy as np, json, sys
from fit_common import *
t = Tpl()
T = np.load(FIT + 'target_raw.npz'); tv, tf = T['v'].astype(np.float64), T['f'].astype(np.int64)
hits = json.load(open(FIT + 'lm_hits.json'))
names = [k for k in hits if k != 'forehead_top' and hits[k]]
src = np.array([hits[k] for k in names]); dst = np.array([t.basis[t.lm_idx[t.lm_names.index(k)]] for k in names])
s, R, tr = umeyama(src, dst); print('Umeyama scale', s)
res0 = np.linalg.norm((s * src @ R.T + tr) - dst, axis=1) * 1e3
# refine: scaled rigid ICP template-face -> target, trimmed
sk = (t.rw >= 1.0)
for k in ('eye_socket', 'mouth_interior', 'nostril'): sk[t.S(k)] = False
cur = (s, R, tr)
tsurf_raw = Surf(tv, tf, 600_000)
q = t.basis[sk]
for it in range(40):
    s_, R_, t_ = cur  # target->template; invert to move template into target space
    inv = lambda p: ((p - t_) @ R_) / s_
    qt = inv(q); d, nn, _ = tsurf_raw.query(qt)
    keep = d <= np.percentile(d, 70)
    # update: map matched target pts to template frame, fit similarity target->template on kept pairs
    s2, R2, t2 = umeyama(nn[keep], q[keep], True)
    # damp scale change: limit +-15% total from Umeyama landmark scale
    s2 = np.clip(s2, s * 0.85, s * 1.15)
    delta = abs(s2 - cur[0]) / cur[0] + np.abs(R2 - cur[1]).max()
    cur = (s2, R2, t2)
    if delta < 1e-6: break
s, R, tr = cur; print('ICP scale', s, 'iters', it, 'trim rms mm', np.sqrt((d[keep] ** 2).mean()) * s * 1e3, 'all rms mm', np.sqrt((d ** 2).mean()) * s * 1e3, 'p95 mm', np.percentile(d, 95) * s * 1e3)
av = (s * tv @ R.T + tr)
res = np.linalg.norm((s * src @ R.T + tr) - dst, axis=1) * 1e3
json.dump({'scale': s, 'R': R.tolist(), 't': tr.tolist(), 'landmarks': {n: {'target_raw': src[i].tolist(), 'template': dst[i].tolist(), 'resid_umeyama_mm': res0[i], 'resid_after_icp_mm': res[i]} for i, n in enumerate(names)}}, open(FIT + 'align.json', 'w'), indent=1)
print('landmark residual mm (umeyama / after icp):'); [print(f'  {n:16s} {res0[i]:6.1f} {res[i]:6.1f}') for i, n in enumerate(names)]
# crop: plane at raw target z=-0.39 mapped through transform; also drop verts far below
zc = (s * np.array([0, -0.30, -0.39]) @ R.T + tr)[2]
print('cut z (template frame)', zc, 'neck ring z', t.basis[t.neck_flag][:, 2].min(), t.basis[t.neck_flag][:, 2].max())
np.savez(FIT + 'target_aligned_full.npz', v=av, f=tf, zcut=zc)
keepv = av[:, 2] > zc
fk = keepv[tf].all(1); idx = -np.ones(len(av), int); idx[keepv] = np.arange(keepv.sum())
cv, cf = av[keepv], idx[tf[fk]]
# keep the largest connected component
import scipy.sparse.csgraph as cg
e = edges_of(cf); A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(len(cv),) * 2)
_, wid = np.unique(np.round(cv, 6), axis=0, return_inverse=True); wid = wid.ravel()
e = edges_of(wid[cf]); A = sp.coo_matrix((np.ones(len(e)), (e[:, 0], e[:, 1])), shape=(wid.max() + 1,) * 2)
nc, lab = cg.connected_components(A, directed=False); lab = lab[wid]; big = np.argmax(np.bincount(lab))
kv = lab == big; fk2 = kv[cf].all(1); idx2 = -np.ones(len(cv), int); idx2[kv] = np.arange(kv.sum())
cv, cf = cv[kv], idx2[cf[fk2]]
np.savez(FIT + 'target_head.npz', v=cv, f=cf); print('cropped', cv.shape, cf.shape, cv.min(0), cv.max(0))
