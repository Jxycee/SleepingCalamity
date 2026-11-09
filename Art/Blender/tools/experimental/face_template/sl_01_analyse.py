"""Step 1: analyse the 83 existing head sliders. Writes OUT/analysis.json + analysis.txt"""
import json, numpy as np
from sl_common import *
t = Tpl(); B = t.basis; K = t.keys; N = len(B)
nrm = vertex_normals(B, t.faces)
idx = t.slider_idx; names = t.slider_names
groups = split_groups(t)
mir, mird = mirror_map(t)
E = edges_of(t.rep[t.faces])
lm = {n: B[i] for n, i in zip(t.lm_names, t.lm_idx)}
rings = {n: t.S(n) for n in ['eyelid_ring_L', 'eyelid_ring_R', 'lip_line']}
def ringratio(v, ring):
    r = np.asarray(ring); 
    e = [(a, b) for a, b in E if a in set(r) and b in set(r)]
    return e
ringE = {n: np.array([(a, b) for a, b in E if a in set(map(int, r)) and b in set(map(int, r))]) for n, r in rings.items()}
D = {}
res = []
Dm = {}
for nm, ki in zip(names, idx):
    d = (K[ki] - B) * 1000.0   # mm
    mag = np.linalg.norm(d, axis=1)
    sup = mag > 0.05
    D[nm] = d
    r = dict(name=nm, key=int(ki), support=int(sup.sum()), max_mm=float(mag.max()), mean_mm=float(mag[sup].mean()) if sup.any() else 0.0)
    if sup.any():
        P = B[sup]; r['bbox_min'] = (P.min(0)).round(4).tolist(); r['bbox_max'] = P.max(0).round(4).tolist()
        w = mag[sup]; r['centroid'] = ((P * w[:, None]).sum(0) / w.sum()).round(4).tolist()
        mu = (d[sup]).mean(0); r['mean_dir'] = (mu / (np.linalg.norm(mu) + 1e-12)).round(3).tolist()
        r['dir_coherence'] = float(np.linalg.norm((d[sup] / mag[sup, None]).mean(0)))   # 1 = pure translation
        nd = (d[sup] * nrm[sup]).sum(1) / mag[sup]; r['abs_cos_normal'] = float(np.abs(nd).mean())
        # falloff: displacement ratio vs distance from peak vertex
        pk = int(mag.argmax()); dist = np.linalg.norm(B[sup] - B[pk], axis=1) * 1000
        ratio = mag[sup] / mag.max()
        R = dist.max()
        r['support_radius_mm'] = float(R)
        bins = np.linspace(0, R, 9); prof = []
        for a, b in zip(bins[:-1], bins[1:]):
            m = (dist >= a) & (dist < b + 1e-9)
            prof.append(float(np.percentile(ratio[m], 90)) if m.any() else None)
        r['falloff_p90_8bins'] = prof
        # fit gaussian sigma on envelope: sigma from half-ratio
        # symmetry (x-mirror): L deltas vs mirrored R
        dm = d[mir] * np.array([-1, 1, 1])
        r['mirror_cos'] = float((d * dm).sum() / (np.linalg.norm(d) * np.linalg.norm(dm) + 1e-12))
        # seam tie
        sd = max([np.abs(d[g] - d[g[0]]).max() for g in groups if len(g) > 1])
        r['seam_max_split_mm'] = float(sd)
        # ring edge-length ratio change
        rr = {}
        for rn, e in ringE.items():
            l0 = np.linalg.norm(B[e[:, 0]] - B[e[:, 1]], axis=1); l1 = np.linalg.norm(K[ki][e[:, 0]] - K[ki][e[:, 1]], axis=1)
            q = l1 / l0; rr[rn] = [float(q.min()), float(q.max())]
        r['ring_edge_ratio'] = rr
        r['ring_move_mm'] = {rn: float(mag[np.asarray(ri)].max()) for rn, ri in rings.items()}
        # smoothness: max edge delta jump
        r['hard_edge'] = float(np.linalg.norm(d[E[:, 0]] - d[E[:, 1]], axis=1).max())
    res.append(r)
# cosine matrix over sliders
M = np.stack([D[n].ravel() for n in names]); Mn = M / (np.linalg.norm(M, axis=1)[:, None] + 1e-12)
C = Mn @ Mn.T; np.fill_diagonal(C, 0)
dup = []
for i in range(len(names)):
    for j in range(i + 1, len(names)):
        if abs(C[i, j]) > 0.6: dup.append([names[i], names[j], float(C[i, j])])
# +/- pair detection
pairs = {}
for i in range(len(names)):
    j = int(np.abs(C[i]).argmax()); pairs[names[i]] = [names[j], float(C[i, j])]
# ARKit / viseme keys vs sliders: overlap
ak = {}
for kn, ki in zip(t.names, range(len(t.names))):
    pass
other = [(n, i) for i, n in enumerate(t.names) if i > 0 and i not in set(idx)]
ov = {}
for n, i in other:
    v = ((K[i] - B) * 1000).ravel(); v = v / (np.linalg.norm(v) + 1e-12)
    c = Mn @ v; j = int(np.abs(c).argmax()); ov[n] = [names[j], float(c[j])]
# extras
extras = dict(body=[str(x) for x in t.z['body/key_names']], eyeL=[str(x) for x in t.z['eyeL/key_names']],
              teethLower=[str(x) for x in t.z['teethLower/key_names']], teethUpper=[str(x) for x in t.z['teethUpper/key_names']],
              lashes=[str(x) for x in t.z['lashes/key_names']])
nmax = max(r['max_mm'] for r in res)
J = dict(sliders=res, near_duplicates=dup, best_partner=pairs, nonslider_key_best_slider=ov, extras=extras,
         mirror_vertex_dist_max_mm=float(mird.max() * 1000), mirror_dist_p99_mm=float(np.percentile(mird, 99) * 1000), global_max_mm=nmax)
json.dump(J, open(OUT + 'analysis.json', 'w'), indent=1)
L = []
L.append('%-16s %5s %6s %6s %5s %5s %5s %6s %6s' % ('slider', 'supp', 'max', 'mean', 'coh', 'nrm', 'mirr', 'seam', 'hard'))
for r in res:
    if r['support'] == 0: L.append(r['name'] + ' EMPTY'); continue
    L.append('%-16s %5d %6.2f %6.2f %5.2f %5.2f %5.2f %6.3f %6.2f R=%5.1f' % (r['name'], r['support'], r['max_mm'], r['mean_mm'], r['dir_coherence'], r['abs_cos_normal'], r['mirror_cos'], r['seam_max_split_mm'], r['hard_edge'], r['support_radius_mm']))
L.append('near-duplicates (|cos|>0.6):'); L += ['  %s ~ %s %.2f' % tuple(x) for x in dup]
L.append('nonslider keys with highest slider overlap:'); L += ['  %s ~ %s %.2f' % (k, v[0], v[1]) for k, v in ov.items() if abs(v[1]) > 0.3]
open(OUT + 'analysis.txt', 'w').write('\n'.join(L))
print('\n'.join(L))
print(extras['teethLower'], extras['eyeL'], len(extras['lashes']))
print('mirror dist max/p99 mm', J['mirror_vertex_dist_max_mm'], J['mirror_dist_p99_mm'])
