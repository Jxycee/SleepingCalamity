"""Round 6: additional eye sliders (SC_Eye_Almond, SC_UpperLid_Heavy, SC_Eye_Width), same recipe/format as sl_make_sliders. -> OUT/eye_sliders6.npz"""
import json, numpy as np
from scipy.spatial import cKDTree
from sl_common import *

t = Tpl(); B = t.basis; N = len(B); rep = t.rep
P = B[rep] * 1000.0                     # mm, tied per weld group
x, y, z = P[:, 0], P[:, 1], P[:, 2]
ss = smoothstep
Rf = rep[t.faces]; Eg = edges_of(Rf)
W = np.linalg.norm(P[Eg[:, 0]] - P[Eg[:, 1]], axis=1)
G = sp.coo_matrix((np.r_[W, W], (np.r_[Eg[:, 0], Eg[:, 1]], np.r_[Eg[:, 1], Eg[:, 0]])), shape=(N, N)).tocsr()

def S(n): return np.unique(rep[t.S(n)])
def geo(core, R=np.inf):
    d = csg.dijkstra(G, directed=False, indices=np.asarray(core), min_only=True, limit=R)
    return d[rep]
def fall(d, R, inner=0.0): return 1 - ss((d - inner) / (R - inner))
def lm(n): return int(rep[t.lm_idx[t.lm_names.index(n)]])
def within(idx, r): return np.unique(np.where(np.linalg.norm(P - P[idx], axis=1) < r)[0])
def protect(sets, margin):
    c = np.unique(np.concatenate(sets)); return ss(geo(c, margin + 1) / margin)
tree_all = cKDTree(P)
def extrap(core_mask, fn):
    """field defined on the core (fn(points)->(N,3)), extended to the rest by nearest-core-vertex value."""
    ci = np.where(core_mask)[0]; _, nn = cKDTree(P[ci]).query(P); return fn(P[ci[nn]]), ci[nn]
sideL = x > 0; sideR = x < 0
ring = {'L': S('eyelid_ring_L'), 'R': S('eyelid_ring_R')}
sock = S('eye_socket')
eye_core = {s: np.unique(np.concatenate([ring[s], sock[(P[sock, 0] > 0) == (s == 'L')]])) for s in 'LR'}
eye_c = {s: P[ring[s]].mean(0) for s in 'LR'}
lipL = S('lip_line'); nostril = S('nostril'); mouth_in = np.unique(np.concatenate([S('mouth_interior'), S('lip_patch_upper_verts'), S('lip_patch_lower_verts')]))
all_eye = np.concatenate([eye_core['L'], eye_core['R']])

# y used for "front" weights: mouth interior rides with the lips instead of fading with its depth
yf = y.copy(); yf[mouth_in] = np.minimum(yf[mouth_in], -110.0)
Wf_face = lambda lo, hi: ss((-yf - lo) / (hi - lo))
FN = ss((z - 1550) / 45.0)                    # keeps neck ring (z<=1550) fixed

def D3(): return np.zeros((N, 3))
F = {}   # name -> (delta mm at amount +1)
upper = {s: S('eyelid_ring_%s_upper' % s) for s in 'LR'}; lower = {s: S('eyelid_ring_%s_lower' % s) for s in 'LR'}
innerc = {s: lm('eye_inner_' + s) for s in 'LR'}; outerc = {s: lm('eye_outer_' + s) for s in 'LR'}
# ---- almond: rigid in-plane rotation of the lid block about the ring centroid (outer corner up, inner corner down); amount +1 = 0.075 rad (4.3 deg)
d = D3(); K = 0.075
for s in 'LR':
    sg = 1 if s == 'L' else -1; c = eye_c[s]; wt = fall(geo(eye_core[s], 22), 22) * (np.sign(x) == sg)
    dx = (x - c[0]) * sg; dz = z - c[2]            # dx>0 toward the outer side
    d[:, 2] += K * dx * wt; d[:, 0] += -K * dz * wt * sg       # rotation: outer side up
F['Eye_Almond'] = d
# ---- width: x-scale about the ring centroid (saturated radius), z untouched; amount +1 = +0.13 of the half width
d = D3()
for s in 'LR':
    sg = 1 if s == 'L' else -1; c = eye_c[s]; wt = fall(geo(eye_core[s], 22), 22) * (np.sign(x) == sg)
    u = (x - c[0]); d[:, 0] += 0.13 * u * np.minimum(1.0, 16.0 / np.maximum(np.abs(u), 1e-9)) * wt
F['Eye_Width'] = d
# ---- heavy upper lid: upper-lid ring and the skin above it move down (towards the lower lid), corners half; amount +1 = 0.7 mm at the ring
d = D3()
for s in 'LR':
    sg = 1 if s == 'L' else -1; c = eye_c[s]
    gu = geo(upper[s], 9); wu = np.where(np.isfinite(gu), fall(gu, 9), 0) * (np.sign(x) == sg) * (z > c[2] - 3.0)
    cor = np.abs(x - c[0]) / 11.3; wc = 1 - 0.65 * ss((cor - 0.55) / 0.45)          # corners move less
    d[:, 2] += -0.7 * wu * wc
F['UpperLid_Heavy'] = d
names = []; deltas = []
for k, f in F.items():
    if k not in ('Eye_Almond', 'Eye_Width', 'UpperLid_Heavy'): continue
    for sgn, tag in ((1, '+'), (-1, '-')):
        dd = (f * sgn)[rep]; dd[t.S('neck_ring_raw')] = 0
        names.append('SC_%s%s' % (k, tag)); deltas.append(dd / 1000.0)
deltas = np.stack(deltas).astype(np.float32); masks = np.stack([np.linalg.norm(dl, axis=1) > 5e-5 for dl in deltas])
np.savez_compressed(OUT + 'eye_sliders6.npz', names=np.array(names), delta=deltas, support=masks, basis=B.astype(np.float32))
for n, dl in zip(names, deltas):
    m = np.linalg.norm(dl, axis=1) * 1000; print('%-22s max %.2f mm  support %d' % (n, m.max(), (m > 0.05).sum()))
