"""Re-seat eyeballs and teeth on the wrapped / slider-only head (venv python; numpy + fit_common).
Eyeball: translate by the eyelid-ring centroid change + uniform scale by the ring-size change (about the eyeball centre), per eye.
Teeth: similarity (rotation + translation + uniform scale) fitted on mouth corners + lip-line verts (upper/lower line for each jaw).
Writes fit/face2_head_variants.npz (head basis/keys per variant) and fit/face2_parts.npz (transformed eye/teeth basis+keys), fit/face2_reseat.json."""
import sys, os; sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import numpy as np, json
from fit_common import *
t = Tpl()
old = t.basis
full_b = np.load(FIT + 'wrap_r3_basis.npy').astype(np.float64); full_k = np.load(FIT + 'wrap_r3_keys.npy').astype(np.float64)
sl_b = np.load(FIT + 'slider_verts_lam0.001.npy').astype(np.float64)
sl_k = t.keys + (sl_b - old)[None]; sl_k[0] = sl_b
z = t.z
out = {}; rep = {}
np.savez_compressed(D + 'fit/face2_head_variants.npz', full_basis=full_b.astype(np.float32), full_keys=full_k.astype(np.float32),
                    slider_basis=sl_b.astype(np.float32), slider_keys=sl_k.astype(np.float32))
bone_names = list(z['bone_names']); bh = z['bone_heads_world']
def ring_stats(P, idx):
    p = P[idx]; c = p.mean(0); return c, np.linalg.norm(p - c, axis=1).mean()
def apply(P, s, R, tt, c0):  # similarity about nothing: p' = s R p + t
    return (P @ R.T) * s + tt
for var, newb in (('full', full_b), ('slider', sl_b)):
    r = {}
    for side, ek in (('L', 'eyeL'), ('R', 'eyeR')):
        ring = t.S('eyelid_ring_' + side).astype(int)
        c0, s0 = ring_stats(old, ring); c1, s1 = ring_stats(newb, ring)
        sc = float(np.clip(s1 / s0, 0.8, 1.25)); dc = c1 - c0
        eb = z[ek + '/basis'].astype(np.float64); ekeys = z[ek + '/keys'].astype(np.float64)
        e0 = eb.mean(0)
        f = lambda P: e0 + dc + sc * (P - e0)
        out[f'{var}/{ek}/basis'] = f(eb).astype(np.float32); out[f'{var}/{ek}/keys'] = f(ekeys).astype(np.float32)
        bn = ('Left' if side == 'L' else 'Right') + 'Eye'; b0 = bh[bone_names.index(bn)]
        r[ek] = dict(ring_centroid_old=c0.tolist(), ring_centroid_new=c1.tolist(), ring_size_old_mm=float(s0 * 1e3), ring_size_new_mm=float(s1 * 1e3),
                     scale_raw=float(s1 / s0), scale_used=sc, eyeball_translate_mm=(dc * 1e3).tolist(),
                     eye_bone=bn, eye_bone_head_old=b0.tolist(), eye_bone_head_offset_mm=(dc * 1e3).tolist(),
                     eye_bone_head_new_if_follow_eyeball=(f(b0[None])[0]).tolist())
    corners = [t.lm_idx[t.lm_names.index('mouth_corner_L')], t.lm_idx[t.lm_names.index('mouth_corner_R')]]
    for tk, line in (('teethUpper', t.S('lip_line_upper')), ('teethLower', t.S('lip_line_lower'))):
        pts = np.concatenate([corners, line.astype(int)])
        w = np.concatenate([np.full(2, len(line) / 4.0), np.ones(len(line))])  # corners weigh like a quarter of the line each
        s_, R_, t_ = umeyama(old[pts], newb[pts], True, w)
        s_u = float(np.clip(s_, 0.8, 1.25))
        if s_u != s_: t_ = newb[pts].mean(0) - s_u * R_ @ old[pts].mean(0)   # keep centroids matched after clamping
        P = z[tk + '/basis'].astype(np.float64); K = z[tk + '/keys'].astype(np.float64)
        g = lambda X: (X @ R_.T) * s_u + t_
        out[f'{var}/{tk}/basis'] = g(P).astype(np.float32); out[f'{var}/{tk}/keys'] = g(K).astype(np.float32)
        res = np.linalg.norm(g(old[pts]) - newb[pts], axis=1) * 1e3
        ang = float(np.degrees(np.arccos(np.clip((np.trace(R_) - 1) / 2, -1, 1))))
        r[tk] = dict(scale_raw=float(s_), scale_used=s_u, rot_deg=ang, translate_mm=(t_ * 1e3).tolist(), fit_resid_rms_mm=float(np.sqrt((res ** 2).mean())),
                     centroid_shift_mm=((newb[pts].mean(0) - old[pts].mean(0)) * 1e3).tolist())
    rep[var] = r
np.savez_compressed(D + 'fit/face2_parts.npz', **out)
json.dump(rep, open(D + 'fit/face2_reseat.json', 'w'), indent=1)
print(json.dumps(rep, indent=1))
