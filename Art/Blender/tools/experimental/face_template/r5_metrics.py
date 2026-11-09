"""Round-4: face-skin point-to-surface distance to the Hunyuan 3D target (same masks as r2_05_wrap). usage: r5_metrics.py basis.npy [basis.npy ...]  (also importable: tgt_stats(P))"""
import sys, json
from r5_common import *
_t = Tpl(); _H = np.load(D2 + 'fit/target_head_clean.npz'); _surf = Surf(_H['v'], _H['f'], 1_200_000)
_zc = float(np.load(D2 + 'fit/target_aligned_full.npz')['zcut'])
def _masks():
    t = _t; coreraw = np.zeros(len(t.basis), bool)
    for k in ('eye_socket', 'mouth_interior', 'nostril'): coreraw[t.S(k)] = True
    for k in ('eyelid_ring_L', 'eyelid_ring_R'): coreraw[cKDTree(t.basis[t.S(k)]).query_ball_point(t.basis, 0.004, return_length=True) > 0] = True
    face = (t.rw == 1.0) & (t.basis[:, 2] > _zc + 0.01); exc = coreraw.copy()
    for k in ('lip_line', 'lip_seam_all', 'lip_seam_back', 'eyelid_ring_L', 'eyelid_ring_R'): exc[cKDTree(t.basis[t.S(k)]).query_ball_point(t.basis, 0.008, return_length=True) > 0] = True
    near = lambda n, r: np.linalg.norm(t.basis - t.basis[t.lm_idx[t.lm_names.index(n)]], axis=1) < r
    return dict(face=face, skin=face & ~exc, nose=near('nose_tip', 0.022) & face, brow=(near('brow_center', 0.02) | near('glabella', 0.02)) & face,
                lips=(cKDTree(t.basis[t.S('lip_line')]).query_ball_point(t.basis, 0.012, return_length=True) > 0) & face)
M = _masks()
def tgt_stats(P):
    P = np.asarray(P, float); o = {}
    for k, m in M.items(): a, b = p2s_stats(P, _surf, m); o[k + '_rms'] = round(a, 3); o[k + '_p95'] = round(b, 3)
    return o
if __name__ == '__main__':
    for p in sys.argv[1:]: print(p.split('/')[-1], tgt_stats(np.load(p)))
