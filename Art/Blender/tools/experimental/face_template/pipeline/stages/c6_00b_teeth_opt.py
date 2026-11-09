"""Round 3: numeric teeth test + least-squares-style correction of the teeth placement (numpy).
Test: generalized winding number of every tooth vertex against the closed mouth shell (mouth_interior + both lip patches, capped by a fan on the lip-line loop);
'outside' = |wn| < 0.5. Counted at neutral and jawOpen 0.5, template vs result. Pass = result <= template + 10% (per jaw, per state).
If a jaw fails, a similarity correction h(X) = c + s (X - c) + d (scale about the teeth centroid, offset d) is searched per jaw (coordinate descent on a grid, objective =
outside count at both states + a small penalty on |d|, |s-1|), then written back into bake6/parts.npz (the nominal copy is kept as parts_nominal.npz).
usage: c6_00b_teeth_opt.py [--apply]"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json, shutil
from c6_common import *
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
def _wn_np(P, Q):
    cen = P[lv].mean(0)
    A = np.concatenate([P[SHELL[:, 0]], P[bv]]); B = np.concatenate([P[SHELL[:, 1]], P[bu]]); C = np.concatenate([P[SHELL[:, 2]], np.repeat(cen[None], len(bu), 0)])
    out = np.zeros(len(Q))
    for i0 in range(0, len(Q), 500):
        q = Q[i0:i0 + 500][:, None, :]; a = A[None] - q; b = B[None] - q; c = C[None] - q
        la, lb, lc = np.linalg.norm(a, axis=2), np.linalg.norm(b, axis=2), np.linalg.norm(c, axis=2)
        num = (a * np.cross(b, c)).sum(2); den = la * lb * lc + (a * b).sum(2) * lc + (b * c).sum(2) * la + (c * a).sum(2) * lb
        out[i0:i0 + 500] = (2 * np.arctan2(num, den)).sum(1) / (4 * np.pi)
    return out
_GPU = {'ok': None}
def _gpu_ready():
    """torch float64 winding number on CUDA (round 15: ~100x faster, agrees with numpy to ~1e-14, identical inside/outside counts). SCP_WN_CPU=1 forces numpy; any import/CUDA/VRAM problem falls back to numpy."""
    if _GPU['ok'] is None:
        ok = _os.environ.get('SCP_WN_CPU', '') in ('', '0')
        if ok:
            try:
                import torch
                ok = bool(torch.cuda.is_available()) and torch.cuda.mem_get_info()[0] > 1.5e9   # chunk of 1000 queries x ~1500 tris needs ~0.5 GB
            except Exception: ok = False
        _GPU['ok'] = ok
        print('winding number on', 'GPU (torch float64)' if ok else 'CPU (numpy)', flush=True)
    return _GPU['ok']
def _wn_gpu(P, Q):
    import torch
    dev, dt = 'cuda', torch.float64
    cen = P[lv].mean(0)
    A = np.concatenate([P[SHELL[:, 0]], P[bv]]); B = np.concatenate([P[SHELL[:, 1]], P[bu]]); C = np.concatenate([P[SHELL[:, 2]], np.repeat(cen[None], len(bu), 0)])
    A, B, C = [torch.as_tensor(x, dtype=dt, device=dev) for x in (A, B, C)]
    out = np.zeros(len(Q))
    for i0 in range(0, len(Q), 1000):
        q = torch.as_tensor(Q[i0:i0 + 1000], dtype=dt, device=dev)[:, None, :]; a = A[None] - q; b = B[None] - q; c = C[None] - q
        la, lb, lc = a.norm(dim=2), b.norm(dim=2), c.norm(dim=2)
        num = (a * torch.cross(b, c, dim=2)).sum(2); den = la * lb * lc + (a * b).sum(2) * lc + (b * c).sum(2) * la + (c * a).sum(2) * lb
        out[i0:i0 + 1000] = ((2 * torch.atan2(num, den)).sum(1) / (4 * np.pi)).cpu().numpy()
    return out
def wn(P, Q):
    if _gpu_ready():
        try: return _wn_gpu(P, Q)
        except Exception as e:   # e.g. CUDA out of memory because ComfyUI grew: fall back, stay correct
            print('GPU winding number failed (%s); using numpy' % str(e)[:120], flush=True); _GPU['ok'] = False
    return _wn_np(P, Q)
kn = [str(x) for x in z['head/key_names']]; ji = kn.index('jawOpen')
STATES = {'neutral': (None, 0), 'jaw0.5': ('jawOpen', 0.5), 'aa': ('aa', 1.0), 'oh': ('oh', 1.0)}   # round 6: aa / oh are judged too
def head_state(basis, keys, st):
    k, w = STATES[st]; return basis if k is None else basis + w * (keys[kn.index(k)] - basis)
def teeth_state(b, k, names, st):
    kk, w = STATES[st]
    return b if (kk is None or kk not in names) else b + w * (k[names.index(kk)] - b)
def counts(head_b, head_k, tb, tk, names, idx=None):
    out = {}
    for st in STATES:
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
    # round 4: ONE similarity (uniform scale s >= 0.97, translation, small rotation) for both jaws together, so the teeth keep the template-relative size
    if not os.path.exists(NOMF): shutil.copy(B2 + 'parts.npz', NOMF)
    hb, hk = hv['full_basis'].astype(float), hv['full_keys'].astype(float)
    TK = ('teethUpper', 'teethLower'); tb = {k: pp[f'full/{k}/basis'].astype(float) for k in TK}; tkeys = {k: pp[f'full/{k}/keys'].astype(float) for k in TK}
    c0 = np.concatenate([tb[k] for k in TK]).mean(0)
    def rotm(r):
        a, b, c = np.radians(r); Rx = np.array([[1, 0, 0], [0, np.cos(a), -np.sin(a)], [0, np.sin(a), np.cos(a)]]); Ry = np.array([[np.cos(b), 0, np.sin(b)], [0, 1, 0], [-np.sin(b), 0, np.cos(b)]]); Rz = np.array([[np.cos(c), -np.sin(c), 0], [np.sin(c), np.cos(c), 0], [0, 0, 1]])
        return Rz @ Ry @ Rx
    def mk(p):
        Rm = rotm(p[4:7]); d = np.array(p[1:4]) * 1e-3; return lambda X: c0 + p[0] * ((X - c0) @ Rm.T) + d
    # round 6: SEPARATE similarity per jaw (scale >= 0.97, offsets <= 3 mm), search on a 1/3 subsample of the tooth vertices, all four states (neutral, jawOpen .5, aa, oh)
    SUB = {k: np.arange(0, len(tb[k]), 3) for k in TK}
    def tot(p, k, sub=True):
        h = mk(p); idx = SUB[k] if sub else None
        c = counts(hb, hk, h(tb[k]), h(tkeys[k]), names[k], idx); return sum(c.values()) * (3 if sub else 1)
    def score(p, k): return tot(p, k) + 0.3 * np.abs(p[1:4]).sum() + 20 * abs(p[0] - 1) + 2 * np.abs(p[4:7]).sum() + (1e4 if p[0] < 0.97 else 0)
    P6 = {}; fin = {}
    for k in TK:
        p = np.array([1.0, 0, 0, 0, 0, 0, 0]); best = score(p, k)
        for sweep in range(2):
            for i, st in enumerate((0.01, 0.5, 0.5, 0.5, 0.7, 0.7, 0.7)):
                for sgn in (-1, 1):
                    for mult in (1, 2, 3, 4):
                        q = p.copy(); q[i] += sgn * st * mult
                        if q[0] < 0.97 or q[0] > 1.05 or np.abs(q[1:4]).max() > 3.0 or np.abs(q[4:7]).max() > 4.0: continue
                        sc = score(q, k)
                        if sc < best: best, p = sc, q
            print(k, 'sweep', sweep, 'p', p.round(3), 'score', round(best, 1), flush=True)
        P6[k] = p
    fin['similarity'] = {k: dict(scale=float(P6[k][0]), offset_mm=[float(v) for v in P6[k][1:4]], rot_deg=[float(v) for v in P6[k][4:7]]) for k in TK}
    for k in TK:
        h = mk(P6[k])
        pp[f'full/{k}/basis'] = h(tb[k]).astype(np.float32); pp[f'full/{k}/keys'] = h(tkeys[k]).astype(np.float32)
        fin[k] = counts(hb, hk, h(tb[k]), h(tkeys[k]), names[k])
    np.savez_compressed(B2 + 'parts.npz', **pp); res['final'] = fin; print('final', fin)
json.dump(res, open(B2 + 'teeth_counts.json', 'w'), indent=1)
