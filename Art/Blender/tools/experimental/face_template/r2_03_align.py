# Round 2 step 1: mediapipe-landmark Umeyama (both meshes raycast through the same detector) + landmark-augmented trimmed scaled ICP.
import numpy as np, json, sys
from r2_common import *
t = Tpl()
T = np.load(FIT + 'target_raw.npz'); tv, tf = T['v'].astype(np.float64), T['f'].astype(np.int64)
ht = json.load(open(FIT + 'tgt_hits.json')); hp = json.load(open(FIT + 'tpl_hits.json'))
ids = [k for k in ht if ht[k] and hp.get(k)]
src = np.array([ht[k] for k in ids]); dst = np.array([hp[k] for k in ids])
print('landmark pairs', len(ids))
# --- Umeyama with iterative outlier rejection
keep = np.ones(len(ids), bool)
for it in range(6):
    s, R, tr = umeyama(src[keep], dst[keep])
    r = np.linalg.norm(s * src @ R.T + tr - dst, axis=1)
    keep = r < max(2.5 * np.median(r[keep]), 0.004)
print('Umeyama scale', s, 'kept', keep.sum(), 'resid mm median/p95/max', np.median(r) * 1e3, np.percentile(r, 95) * 1e3, r.max() * 1e3)
res0 = r.copy()
# --- ICP on face skin (template frame <- target) with landmark pairs added at fixed total weight
sk = (t.rw >= 1.0)
for k in ('eye_socket', 'mouth_interior', 'nostril'): sk[t.S(k)] = False
chin_z = t.basis[t.lm_idx[t.lm_names.index('chin_tip')]][2]
sk &= t.basis[:, 2] > chin_z + 0.005
q = t.basis[sk]; surf = Surf(tv, tf, 800_000)
cur = (s, R, tr); LMW = float(sys.argv[1]) if len(sys.argv) > 1 else 0.3
for it in range(60):
    s_, R_, t_ = cur; inv = lambda p: ((p - t_) @ R_) / s_
    d, nn, _ = surf.query(inv(q)); kp = d <= np.percentile(d, 70)
    P_t = np.concatenate([nn[kp], src[keep]]); P_s = np.concatenate([q[kp], dst[keep]])
    w = np.concatenate([np.full(kp.sum(), (1 - LMW) / kp.sum()), np.full(keep.sum(), LMW / keep.sum())])
    s2, R2, t2 = umeyama(P_t, P_s, True, w); s2 = np.clip(s2, s * 0.85, s * 1.15)
    delta = abs(s2 - cur[0]) / cur[0] + np.abs(R2 - cur[1]).max(); cur = (s2, R2, t2)
    if delta < 1e-7: break
s, R, tr = cur
res = np.linalg.norm(s * src @ R.T + tr - dst, axis=1)
d, nn, _ = surf.query(((q - tr) @ R) / s)
print('ICP scale', s, 'iters', it, 'trim rms mm', np.sqrt((d[kp] ** 2).mean()) * s * 1e3, 'all rms', np.sqrt((d ** 2).mean()) * s * 1e3, 'p95', np.percentile(d, 95) * s * 1e3)
print('landmark resid mm after ICP: median %.2f p95 %.2f max %.2f (inliers median %.2f)' % (np.median(res) * 1e3, np.percentile(res, 95) * 1e3, res.max() * 1e3, np.median(res[keep]) * 1e3))
# eye-line orientation + yaw check from landmark pairs 33/263 (outer eye) and 61/291
def ang(a, b): v = b - a; return np.degrees(np.arctan2(v[2], v[0]))
def idx(k): return ids.index(str(k))
mp_ = lambda k: (s * src[idx(k)] @ R.T + tr)
eye_t = ang(mp_(33), mp_(263)); eye_p = ang(dst[idx(33)], dst[idx(263)])
yaw_t = np.degrees(np.arctan2((mp_(263) - mp_(33))[1], (mp_(263) - mp_(33))[0])); yaw_p = np.degrees(np.arctan2((dst[idx(263)] - dst[idx(33)])[1], (dst[idx(263)] - dst[idx(33)])[0]))
print('eye-line roll deg target(after) %.2f template %.2f ; yaw-ish %.2f / %.2f' % (eye_t, eye_p, yaw_t, yaw_p))
av = s * tv @ R.T + tr
zc = chin_z - float(sys.argv[2]) if len(sys.argv) > 2 else chin_z - 0.02
json.dump({'scale': s, 'R': R.tolist(), 't': tr.tolist(), 'zcut': zc, 'lm_ids': ids, 'kept': keep.tolist(), 'resid_umeyama_mm': (res0 * 1e3).tolist(), 'resid_icp_mm': (res * 1e3).tolist(),
           'eye_roll_deg': [eye_t, eye_p], 'icp_trim_rms_mm': float(np.sqrt((d[kp] ** 2).mean()) * s * 1e3), 'lm_weight': LMW}, open(FIT + 'align.json', 'w'), indent=1)
worst = np.argsort(-res)[:8]; print('worst landmarks', [(ids[i], round(res[i] * 1e3, 1)) for i in worst])
np.savez(FIT + 'target_aligned_full.npz', v=av, f=tf, zcut=zc)
print('chin_z', chin_z, 'zcut', zc, 'aligned bbox', av.min(0), av.max(0))
