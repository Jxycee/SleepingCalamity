"""Round 3: numeric teeth test + least-squares-style correction of the teeth placement (numpy).
Test: generalized winding number of every tooth vertex against the closed mouth shell (mouth_interior + both lip patches, capped by a fan on the lip-line loop);
'outside' = |wn| < 0.5. Counted at neutral and jawOpen 0.5, template vs result. Pass = result <= template + 10% (per jaw, per state).
If a jaw fails, a similarity correction h(X) = c + s (X - c) + d (scale about the teeth centroid, offset d) is searched per jaw (coordinate descent on a grid, objective =
outside count at both states + a small penalty on |d|, |s-1|), then written back into bake3/parts.npz (the nominal copy is kept as parts_nominal.npz).
usage: c3_00b_teeth_opt.py [--apply]"""
import sys, json, shutil
from c3_common import *
t = Tpl(); z = t.z
hv = np.load(B2 + 'head_variants.npz'); import os
NOMF = B2 + 'parts_nominal.npz'; pp = dict(np.load(NOMF if os.path.exists(NOMF) else B2 + 'parts.npz'))
_rep = z['tpl/weld_rep'].astype(int); _hf = z['head/faces'].astype(int)
_vs = np.zeros(len(t.basis), bool)
for _k in ('mouth_interior', 'lip_patch_upper_verts', 'lip_patch_lower_verts'): _vs[z['tpl/' + _k]] = True
SHELL = _hf[_vs[_hf].all(1)]
_dir = {}
for a_, b_, c_ in _rep[SHELL]:
    for u, v in ((a_, b_), (b_, c_), (c_, a_)): _dir[(u, v)] = _dir.get((u, v), 0) + 1
BND = [(u, v) for (u, v) in _dir if (v, u) not in _dir]; LOOPV = sorted({x for e in BND for x in e})
W2V = {}
for i in range(len(_rep)): W2V.setdefault(_rep[i], i)
bu = np.array([W2V[u] for u, v in BND]); bv = np.array([W2V[v] for u, v in BND]); lv = np.array([W2V[v] for v in LOOPV])
def wn(P, Q):
    cen = P[lv].mean(0)
    A = np.concatenate([P[SHELL[:, 0]], P[bv]]); B = np.concatenate([P[SHELL[:, 1]], P[bu]]); C = np.concatenate([P[SHELL[:, 2]], np.repeat(cen[None], len(bu), 0)])
    out = np.zeros(len(Q))
    for i0 in range(0, len(Q), 500):
        q = Q[i0:i0 + 500][:, None, :]; a = A[None] - q; b = B[None] - q; c = C[None] - q
        la, lb, lc = np.linalg.norm(a, axis=2), np.linalg.norm(b, axis=2), np.linalg.norm(c, axis=2)
        num = (a * np.cross(b, c)).sum(2); den = la * lb * lc + (a * b).sum(2) * lc + (b * c).sum(2) * la + (c * a).sum(2) * lb
        out[i0:i0 + 500] = (2 * np.arctan2(num, den)).sum(1) / (4 * np.pi)
    return out
kn = [str(x) for x in z['head/key_names']]; ji = kn.index('jawOpen')
def head_state(basis, keys, st): return basis if st == 'neutral' else basis + 0.5 * (keys[ji] - basis)
def teeth_state(b, k, names, st): return b if st == 'neutral' else b + 0.5 * (k[names.index('jawOpen')] - b) if 'jawOpen' in names else b
def counts(head_b, head_k, tb, tk, names, idx=None):
    out = {}
    for st in ('neutral', 'jaw0.5'):
        P = head_state(head_b, head_k, st); Q = teeth_state(tb, tk, names, st)
        if idx is not None: Q = Q[idx]
        out[st] = int((np.abs(wn(P, Q)) < 0.5).sum())
    return out
res = {}
names = {'teethUpper': [str(x) for x in z['teethUpper/key_names']], 'teethLower': [str(x) for x in z['teethLower/key_names']]}
tpl = {}
for tk in ('teethUpper', 'teethLower'):
    tpl[tk] = counts(t.basis, t.keys, z[tk + '/basis'].astype(float), z[tk + '/keys'].astype(float), names[tk])
nom = {}
for tk in ('teethUpper', 'teethLower'):
    nom[tk] = counts(hv['full_basis'].astype(float), hv['full_keys'].astype(float), pp[f'full/{tk}/basis'].astype(float), pp[f'full/{tk}/keys'].astype(float), names[tk])
print('template', tpl); print('result (nominal reseat)', nom)
res['template'] = tpl; res['nominal'] = nom
if '--apply' in sys.argv:
    if not os.path.exists(NOMF): shutil.copy(B2 + 'parts.npz', NOMF)
    hb, hk = hv['full_basis'].astype(float), hv['full_keys'].astype(float); fin = {}
    rng = np.random.default_rng(0)
    for tk in ('teethUpper', 'teethLower'):
        tb = pp[f'full/{tk}/basis'].astype(float); tkeys = pp[f'full/{tk}/keys'].astype(float); c0 = tb.mean(0)
        sub = np.arange(len(tb))
        def mk(p):
            sc3 = np.array(p[:3]); d = np.array(p[3:]) * 1e-3; return lambda X: c0 + sc3 * (X - c0) + d
        def score(p):
            h = mk(p); c = counts(hb, hk, h(tb), h(tkeys), names[tk], idx=sub)
            return c['neutral'] + c['jaw0.5'] + 0.3 * np.abs(p[3:]).sum() + 20 * np.abs(np.array(p[:3]) - 1).sum()
        p = np.array([1.0, 1.0, 1.0, 0, 0, 0]); best = score(p)
        for sweep in range(3):
            for i, st in enumerate((0.015, 0.015, 0.015, 0.6, 0.6, 0.6)):
                for sgn in (-1, 1):
                    for mult in (1, 2, 3, 4):
                        q = p.copy(); q[i] += sgn * st * mult
                        if np.abs(q[:3] - 1).max() > 0.12 or np.abs(q[3:]).max() > 4.0: continue
                        sc = score(q)
                        if sc < best: best, p = sc, q
            print(tk, 'sweep', sweep, 'p', p.round(3), 'score', round(best, 1))
        h = mk(p)
        pp[f'full/{tk}/basis'] = h(tb).astype(np.float32); pp[f'full/{tk}/keys'] = h(tkeys).astype(np.float32)
        fin[tk] = dict(scale_xyz=[float(v) for v in p[:3]], offset_mm=[float(v) for v in p[3:]], counts=counts(hb, hk, h(tb), h(tkeys), names[tk]))
    np.savez_compressed(B2 + 'parts.npz', **pp); res['final'] = fin; print('final', fin)
json.dump(res, open(B2 + 'teeth_counts.json', 'w'), indent=1)
