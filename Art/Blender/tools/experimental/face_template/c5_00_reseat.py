"""Round 2 step 4 (numpy): head variants + rigid re-seat of eyeballs and teeth on the wrapped / slider-only head.
Eyeball: translation by the eyelid-ring centroid change + uniform scale by ring size change (about the eyeball centre), per eye.
Teeth: similarity fitted on mouth corners + lip-line verts. Eye-bone head offsets are reported only (no re-rig).
Writes bake/head_variants.npz, bake/parts.npz, bake/reseat.json."""
from c5_common import *
import json, os
t = Tpl(); old = t.basis
full_b = np.load(FIT + 'wrap_r' + FINAL_TAG + '_basis.npy').astype(np.float64); full_k = np.load(FIT + 'wrap_r' + FINAL_TAG + '_keys.npy').astype(np.float64)
sl_b = np.load(FIT + 'slider_verts.npy').astype(np.float64)
assert full_k.shape == (150, 7321, 3) and sl_b.shape == (7321, 3) and full_b.shape == (7321, 3)
assert np.abs(full_k[0] - full_b).max() < 1e-4, np.abs(full_k[0] - full_b).max()
# round 5: jaw keys are transferred as plain deltas, so on a shorter lower face the same chin travel opens the mouth too far (teeth then read as a comb). The jaw hinge arm
# (upper lip to chin) is shorter by s = new/old, so the deltas of the jaw-driven ARKit/viseme keys (chin travel >= 3 mm, not cheek/slider keys) are scaled by s (rotation about the hinge).
JAW_SCALE = os.environ.get('JAW_SCALE', '1') == '1'
z = t.z; _kn = [str(x) for x in z['head/key_names']]; _chin = t.lm_idx[t.lm_names.index('chin_tip')]; _lu = t.lm_idx[t.lm_names.index('upper_lip_center')]
JAW_S = float(np.clip((full_b[_lu][2] - full_b[_chin][2]) / (old[_lu][2] - old[_chin][2]), 0.75, 1.0)) if JAW_SCALE else 1.0
_cand = [_kn[i] for i in list(t.z['tpl/key_arkit_idx']) + list(t.z['tpl/key_viseme_idx'])]
JAW_KEYS = [n_ for n_ in _cand if not n_.startswith('cheek') and np.linalg.norm((t.keys[t.kidx(n_)] - old)[_chin]) * 1e3 >= 3.0]
print('jaw key scale %.3f on' % JAW_S, JAW_KEYS)
for n_ in JAW_KEYS:
    i_ = t.kidx(n_); full_k[i_] = full_b + JAW_S * (full_k[i_] - full_b)
sl_k = t.keys + (sl_b - old)[None]; sl_k[0] = sl_b
z = t.z
np.savez_compressed(B2 + 'head_variants.npz', full_basis=full_b.astype(np.float32), full_keys=full_k.astype(np.float32), slider_basis=sl_b.astype(np.float32), slider_keys=sl_k.astype(np.float32))
bone_names = list(z['bone_names']); bh = z['bone_heads_world']
def ring_stats(P, idx):
    p = P[idx]; c = p.mean(0); return c, np.linalg.norm(p - c, axis=1).mean()
import os
MODE = os.environ.get('TEETH_MODE', 'joint')   # indep | joint (one similarity for both jaws) | norot (scale+translate per jaw)
out = {}; rep = {}
for var, newb in (('full', full_b), ('slider', sl_b)):
    r = {}
    for side, ek in (('L', 'eyeL'), ('R', 'eyeR')):
        ring = t.S('eyelid_ring_' + side).astype(int)
        c0, s0 = ring_stats(old, ring); c1, s1 = ring_stats(newb, ring)
        sc = float(np.clip(s1 / s0, 0.8, 1.25)); dc = c1 - c0
        eb = z[ek + '/basis'].astype(np.float64); ekeys = z[ek + '/keys'].astype(np.float64); e0 = eb.mean(0)
        f = lambda P: e0 + dc + sc * (P - e0)
        out[f'{var}/{ek}/basis'] = f(eb).astype(np.float32); out[f'{var}/{ek}/keys'] = f(ekeys).astype(np.float32)
        bn = ('Left' if side == 'L' else 'Right') + 'Eye'; b0 = bh[bone_names.index(bn)]
        r[ek] = dict(ring_size_old_mm=float(s0 * 1e3), ring_size_new_mm=float(s1 * 1e3), scale_raw=float(s1 / s0), scale_used=sc,
                     eyeball_translate_mm=(dc * 1e3).tolist(), eye_bone=bn, eye_bone_head_old=b0.tolist(),
                     eye_bone_head_offset_mm_if_following_eyeball=(dc * 1e3 + (sc - 1) * (b0 - e0) * 1e3).tolist(),
                     eye_bone_head_new_if_following=(f(b0[None])[0]).tolist())
    corners = [t.lm_idx[t.lm_names.index('mouth_corner_L')], t.lm_idx[t.lm_names.index('mouth_corner_R')]]
    allpts = np.concatenate([corners, t.S('lip_line_upper').astype(int), t.S('lip_line_lower').astype(int)])
    sj, Rj, tj = umeyama(old[allpts], newb[allpts], True)
    for tk, line in (('teethUpper', t.S('lip_line_upper')), ('teethLower', t.S('lip_line_lower'))):
        pts = np.concatenate([corners, line.astype(int)])
        w = np.concatenate([np.full(2, len(line) / 4.0), np.ones(len(line))])
        s_, R_, t_ = umeyama(old[pts], newb[pts], True, w)
        if MODE == 'joint': s_, R_, t_ = sj, Rj, tj
        elif MODE == 'norot':
            R_ = np.eye(3); ww = w / w.sum(); a_ = old[pts] - (old[pts] * ww[:, None]).sum(0); b_ = newb[pts] - (newb[pts] * ww[:, None]).sum(0)
            s_ = float((ww * (a_ * b_).sum(1)).sum() / (ww * (a_ ** 2).sum(1)).sum()); t_ = (newb[pts] * ww[:, None]).sum(0) - s_ * (old[pts] * ww[:, None]).sum(0)
        s_u = float(np.clip(s_, 0.8, 1.25))
        if s_u != s_: t_ = newb[pts].mean(0) - s_u * R_ @ old[pts].mean(0)
        P = z[tk + '/basis'].astype(np.float64); K = z[tk + '/keys'].astype(np.float64); tnames = [str(x) for x in z[tk + '/key_names']]
        g = lambda X: (X @ R_.T) * s_u + t_
        Kg = g(K)
        if var == 'full' and tk == 'teethLower':
            for n_ in JAW_KEYS:
                if n_ in tnames: j_ = tnames.index(n_); Kg[j_] = g(P) + JAW_S * (Kg[j_] - g(P))
        out[f'{var}/{tk}/basis'] = g(P).astype(np.float32); out[f'{var}/{tk}/keys'] = Kg.astype(np.float32)
        res = np.linalg.norm(g(old[pts]) - newb[pts], axis=1) * 1e3
        ang = float(np.degrees(np.arccos(np.clip((np.trace(R_) - 1) / 2, -1, 1))))
        r[tk] = dict(scale_raw=float(s_), scale_used=s_u, rot_deg=ang, centroid_shift_mm=((newb[pts].mean(0) - old[pts].mean(0)) * 1e3).tolist(), fit_resid_rms_mm=float(np.sqrt((res ** 2).mean())))
    rep[var] = r
np.savez_compressed(B2 + 'parts.npz', **out); json.dump(rep, open(B2 + 'reseat.json', 'w'), indent=1)
print(json.dumps(rep, indent=1))
