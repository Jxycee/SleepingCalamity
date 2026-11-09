"""Author new head shape keys (delta fields) over the Tobin MetaPerson template.
Recipe inferred from the originals (see docs/research/2026-11-08-character-pipeline.md s.10): region-masked smooth-falloff
(smoothstep) displacement fields; + and - are separate keys (exact negatives of one linear field); seam-coincident vertices
share one displacement (everything is evaluated at the weld representative); neck ring stays 0.
Run: python sl_make_sliders.py  -> OUT/new_sliders.npz (names, delta float32 [N,7321,3] metres, masks, meta)
Units inside: millimetres. Z up, face -Y, character-left +X.
"""
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

# ---- vertical stretches ----
d = D3(); d[:, 2] = -6.0 * ss((1713 - z) / 23.0) * FN * Wf_face(15, 85); F['Midface_Height'] = d
d = D3(); d[:, 2] = -6.5 * ss((1640 - z) / 22.0) * FN * Wf_face(15, 85); F['LowerFace_Height'] = d
d = D3(); d[:, 2] = 7.0 * ss((z - 1752) / 38.0) * ss((-y - 30) / 60.0); F['Forehead_Height'] = d
# ---- cheekbone width ----
cb = {s: within(lm('cheekbone_' + s), 6.0) for s in 'LR'}
w = np.maximum(fall(geo(cb['L'], 45), 42), 0) * sideL + np.maximum(fall(geo(cb['R'], 45), 42), 0) * sideR
w = np.where(np.isfinite(w), w, 0) * protect([all_eye], 6) * protect([lipL, nostril], 7)
d = D3(); d[:, 0] = np.sign(x) * 5.0 * w; d[:, 1] = -1.2 * w; F['Cheekbone_Width'] = d
# ---- eye size (scale in x,z about each eye centre; rigid ring+socket block, saturated radius) ----
d = D3()
for s in 'LR':
    core = np.zeros(N, bool); core[eye_core[s]] = True
    c = eye_c[s]
    def fn(Q, c=c):
        u = (Q - c) * np.array([1, 0, 1]); r = np.linalg.norm(u, axis=1, keepdims=True)
        return np.array([0.22, 0, 0.04]) * u * np.minimum(1.0, 14.0 / np.maximum(r, 1e-9))
    f, _ = extrap(core, fn); d += f * fall(geo(eye_core[s], 22), 22)[:, None] * (np.sign(x) == (1 if s == 'L' else -1))[:, None] * 1.0
F['Eye_Size'] = d
# ---- eye spacing: rigid translation of each eye block toward/away from the midline ----
d = D3()
for s in 'LR':
    sg = 1 if s == 'L' else -1
    d[:, 0] += sg * 3.0 * fall(geo(eye_core[s], 24), 22) * (np.sign(x) == sg)
F['Eye_Spacing'] = d
# ---- lips ----
lz = np.array(sorted(zip(P[lipL, 0], P[lipL, 2])))
zs = np.interp(x, lz[:, 0], lz[:, 1])
wl = np.where(np.isfinite(geo(lipL, 15)), fall(geo(lipL, 15), 14), 0)
Wx = 1 - ss((np.abs(x) - 6) / 18.0)
d = D3(); d[:, 1] = -3.0 * wl * Wx; d[:, 2] = 0.35 * (z - zs) * wl * Wx; F['Lip_Fullness'] = d
wl2 = np.where(np.isfinite(geo(lipL, 23)), fall(geo(lipL, 23), 22), 0)
d = D3(); d[:, 0] = 0.15 * x * wl2; F['Lip_Width'] = d
# ---- brow lift ----
r = np.sqrt((x / 52.0) ** 2 + ((z - 1744) / 17.0) ** 2)
wb = (1 - ss((r - 0.35) / 0.65)) * ss((-y - 80) / 30.0) * protect([all_eye], 9)
d = D3(); d[:, 2] = 5.0 * wb; F['Brow_Lift'] = d
# ---- nose ----
wing = np.unique(np.concatenate([within(lm('nostril_wing_L'), 6.0), within(lm('nostril_wing_R'), 6.0), nostril]))
wn = fall(np.where(np.isfinite(geo(wing, 24)), geo(wing, 24), 99), 22) * protect([lipL], 5)
d = D3(); d[:, 0] = 0.15 * x * wn; F['Nose_Width'] = d
tipc = within(lm('nose_tip'), 14.0)
gt = geo(tipc, 36); wt = np.where(np.isfinite(gt), fall(gt, 34), 0) * protect([lipL], 5) * protect([all_eye], 6)
u = np.minimum(y + 118.0, 0)
d = D3(); d[:, 1] = 0.27 * u * wt; F['Nose_Length'] = d
# ---- ears (size: isotropic about centroid; protrusion: swing out about the front root) ----
d = D3(); d2 = D3()
for s in 'LR':
    ear = S('ear_' + s); core = np.zeros(N, bool); core[ear] = True; c = P[ear].mean(0); sg = 1 if s == 'L' else -1
    wE = np.where(np.isfinite(geo(ear, 20)), fall(geo(ear, 20), 18), 0) * (np.sign(x) == sg)
    f, _ = extrap(core, lambda Q, c=c: 0.15 * (Q - c)); d += f * wE[:, None]
    f2, _ = extrap(core, lambda Q, sg=sg: np.stack([sg * 0.13 * np.clip(Q[:, 1] + 40, 0, 50), 0 * Q[:, 0], 0 * Q[:, 0]], 1))
    d2 += f2 * wE[:, None]
F['Ear_Size'] = d; F['Ear_Protrusion'] = d2
# ---- skull depth (back of head moves, face and neck fixed) ----
d = D3(); d[:, 1] = 8.0 * ss((y + 40) / 140.0) * ss((z - 1560) / 60.0); F['Skull_Depth'] = d

names = []; deltas = []
for k, f in F.items():
    for sgn, tag in ((1, '+'), (-1, '-')):
        dd = (f * sgn)[rep]; dd[t.S('neck_ring_raw')] = 0     # [rep]: every member of a weld group gets the rep's delta
        names.append('SC_%s%s' % (k, tag)); deltas.append((dd[rep] if False else dd) / 1000.0)
deltas = np.stack(deltas).astype(np.float32)
masks = np.stack([np.linalg.norm(dl, axis=1) > 5e-5 for dl in deltas])
np.savez_compressed(OUT + 'new_sliders.npz', names=np.array(names), delta=deltas, support=masks, basis=B.astype(np.float32))
for n, dl in zip(names, deltas):
    m = np.linalg.norm(dl, axis=1) * 1000
    print('%-22s max %.2f mm  support %d' % (n, m.max(), (m > 0.05).sum()))
