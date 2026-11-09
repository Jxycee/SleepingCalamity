"""Shared helpers for the face-template fit spike (numpy/scipy only)."""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import os
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = _P_TPLROOT
FIT = D + 'fit/'

class Tpl:
    def __init__(s):
        z = np.load(os.environ.get('SC_TPL', D + 'template/tobin_head_template.npz'), allow_pickle=True)
        s.z = z
        s.basis = z['head/basis'].astype(np.float64)
        s.keys = z['head/keys'].astype(np.float64)
        s.names = [str(x) for x in z['head/key_names']]
        s.faces = z['head/faces'].astype(np.int64)
        s.rw = z['tpl/region_weight'].astype(np.float64)
        s.rep = z['tpl/weld_rep'].astype(np.int64)
        s.slider_idx = z['tpl/key_slider_idx']
        s.slider_names = [str(x) for x in z['tpl/key_slider_names']]
        s.lm_names = [str(x) for x in z['tpl/landmark_names']]
        s.lm_idx = z['tpl/landmark_idx'].astype(int)
        s.neck_flag = z['tpl/neck_seam_flag']
        s.eyeL = z['eyeL/basis'].astype(np.float64); s.eyeR = z['eyeR/basis'].astype(np.float64)
        s.eyeL_f = z['eyeL/faces']; s.eyeR_f = z['eyeR/faces']
    def S(s, k): return np.asarray(s.z['tpl/' + k])
    def kidx(s, name): return s.names.index(name)

def weld_faces(tpl):
    return tpl.rep[tpl.faces]

def edges_of(faces):
    e = np.concatenate([faces[:, [0, 1]], faces[:, [1, 2]], faces[:, [2, 0]]])
    e = np.sort(e, 1); e = e[e[:, 0] != e[:, 1]]
    return np.unique(e, axis=0)

def count_flips(t, P):
    """faces whose normal is reversed vs the template basis (boolean per face)"""
    def fn(Q):
        tri = Q[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
    return (fn(P) * fn(t.basis)).sum(1) < 0

def repair_flips(t, P, max_rings=8, smooth_iters=40, verbose=True):
    """Generic flip repair for a wrap result P (same topology as the template): the displacement field d = P - basis is Jacobi-smoothed on the WELDED vertex graph
    inside a patch around every flipped face (the patch grows ring by ring until no face is flipped; patch boundary stays fixed, so the repair is local and the fit
    elsewhere is unchanged). Welded copies move together. Returns (P_new, flips_before, flips_after)."""
    import scipy.sparse as sp
    fl0 = count_flips(t, P); n0 = int(fl0.sum())
    if n0 == 0: return P, 0, 0
    nW = int(t.rep.max()) + 1; wf = weld_faces(t); e = edges_of(wf)
    A = sp.coo_matrix((np.ones(len(e) * 2), (np.r_[e[:, 0], e[:, 1]], np.r_[e[:, 1], e[:, 0]])), shape=(nW, nW)).tocsr(); deg = np.maximum(np.asarray(A.sum(1)).ravel(), 1)
    dW = np.zeros((nW, 3)); dW[t.rep] = (P - t.basis)
    seed = np.zeros(nW, bool); seed[np.unique(wf[fl0])] = True
    cur = P
    for rings in range(1, max_rings + 1):
        S = seed.copy()
        for _ in range(rings): S |= (A @ S.astype(float)) > 0
        d = dW.copy(); idx = np.where(S)[0]
        for _ in range(smooth_iters): d[idx] = (A @ d)[idx] / deg[idx, None]
        cur = t.basis + d[t.rep]
        if not count_flips(t, cur).any():
            if verbose: print('repair_flips: %d flipped faces fixed with %d-ring patch (%d verts), max move %.2f mm' % (n0, rings, len(idx), np.abs(cur - P).max() * 1e3))
            return cur, n0, 0
    n1 = int(count_flips(t, cur).sum())
    if verbose: print('repair_flips: %d -> %d flipped faces after %d rings' % (n0, n1, max_rings))
    return cur, n0, n1

def umeyama(src, dst, scale=True, w=None):
    w = np.ones(len(src)) if w is None else w
    w = w / w.sum()
    ms, md = (src * w[:, None]).sum(0), (dst * w[:, None]).sum(0)
    A = (dst - md).T @ ((src - ms) * w[:, None])
    U, S, Vt = np.linalg.svd(A)
    d = np.sign(np.linalg.det(U @ Vt)); Dm = np.diag([1, 1, d])
    R = U @ Dm @ Vt
    var = (w * ((src - ms) ** 2).sum(1)).sum()
    s = (S * np.diag(Dm)).sum() / var if scale else 1.0
    t = md - s * R @ ms
    return s, R, t

class Surf:
    """Dense surface sampling of a triangle mesh: nearest-point queries via cKDTree (no rtree)."""
    def __init__(s, v, f, n=1_200_000, seed=0):
        rng = np.random.default_rng(seed)
        tri = v[f]; nrm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
        area = np.linalg.norm(nrm, axis=1) / 2
        fn = nrm / (2 * area[:, None] + 1e-30)
        vn = np.zeros_like(v); np.add.at(vn, f.ravel(), np.repeat(fn * area[:, None], 3, 0)); vn /= np.linalg.norm(vn, axis=1)[:, None] + 1e-30
        fi = rng.choice(len(f), n, p=area / area.sum())
        r1, r2 = rng.random(n), rng.random(n); sw = r1 + r2 > 1; r1[sw], r2[sw] = 1 - r1[sw], 1 - r2[sw]
        b = np.stack([1 - r1 - r2, r1, r2], 1)
        s.p = np.concatenate([v, (tri[fi] * b[:, :, None]).sum(1)])
        s.n = np.concatenate([vn, (vn[f[fi]] * b[:, :, None]).sum(1)])
        s.n /= np.linalg.norm(s.n, axis=1)[:, None] + 1e-30
        s.tree = cKDTree(s.p)
    def query(s, q):
        d, i = s.tree.query(q); return d, s.p[i], s.n[i]

def vertex_normals(v, f):
    tri = v[f]; fn = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])
    vn = np.zeros_like(v); np.add.at(vn, f.ravel(), np.repeat(fn, 3, 0))
    return vn / (np.linalg.norm(vn, axis=1)[:, None] + 1e-30)

def p2s_stats(v, surf, mask):
    d, _, _ = surf.query(v[mask]); return float(np.sqrt((d ** 2).mean()) * 1e3), float(np.percentile(d, 95) * 1e3)
