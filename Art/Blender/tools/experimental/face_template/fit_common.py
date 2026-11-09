"""Shared helpers for the face-template fit spike (numpy/scipy only)."""
import numpy as np, scipy.sparse as sp
from scipy.spatial import cKDTree
D = 'C:/AI/test_out/heroes/face_spike/'
FIT = D + 'fit/'

class Tpl:
    def __init__(s):
        z = np.load(D + 'template/tobin_head_template.npz', allow_pickle=True)
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
