# Step 4+5: non-rigid wrap (trimesh nricp_amberg on the WELDED mesh) + harmonic displacement fill on the follow set,
# delta transfer to all keys, hard-stop checks.  usage: fit_05_wrap.py <round> <ws_scale> <grow_rings> <pin_follow 0|1> [<lip_data_pin 0|1>]
import numpy as np, json, sys, trimesh, time
import scipy.sparse as sp, scipy.sparse.linalg as spl, scipy.sparse.csgraph as cg
from fit_common import *
from fit_checks import run_checks

alpha = float(sys.argv[5]) if len(sys.argv) > 5 else 0.0  # lid rigidity: blend of eye-region displacement toward its mean (per eye)
rnd = sys.argv[1]; ws_scale = float(sys.argv[2]); grow = int(sys.argv[3]); pin_follow = int(sys.argv[4])
t = Tpl(); H = np.load(FIT + 'target_head.npz'); zc = float(np.load(FIT + 'target_aligned_full.npz')['zcut'])
Ps = np.load(FIT + 'slider_verts_lam0.001.npy').astype(np.float64)

# ---- welded mesh
reps = np.unique(t.rep); newid = -np.ones(len(t.rep), int); newid[reps] = np.arange(len(reps)); w_of = newid[t.rep]  # raw -> welded id
Wv = Ps[reps]; wf = w_of[t.faces]; wf = wf[(wf[:, 0] != wf[:, 1]) & (wf[:, 1] != wf[:, 2]) & (wf[:, 0] != wf[:, 2])]
E = edges_of(wf); nW = len(reps)
ncomp, lab = cg.connected_components(sp.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(nW, nW)), directed=False)
print('welded verts', nW, 'faces', len(wf), 'components', ncomp, np.bincount(lab))
adj = sp.coo_matrix((np.ones(len(E)), (E[:, 0], E[:, 1])), shape=(nW, nW)); adj = (adj + adj.T).tocsr()
def W(idx): return np.unique(w_of[np.asarray(idx)])
def growset(s, n):
    m = np.zeros(nW, bool); m[s] = True
    for _ in range(n): m = m | (adj @ m.astype(float) > 0)
    return m
core = np.zeros(nW, bool)
for k in ('eye_socket', 'mouth_interior', 'nostril', 'eyelid_ring_L', 'eyelid_ring_R', 'lip_line', 'lip_seam_all', 'lip_seam_back'): core[W(t.S(k))] = True
ears = np.zeros(nW, bool); ears[W(t.S('ear_L'))] = True; ears[W(t.S('ear_R'))] = True
follow = growset(np.where(core)[0], grow) | ears
notarget = Wv[:, 2] < zc + 0.01
neck = np.zeros(nW, bool); neck[W(t.S('neck_ring_raw'))] = True
pin = notarget | neck | (core if pin_follow else False)
print('follow', follow.sum(), 'pinned', pin.sum(), 'core', core.sum())

# ---- target (subdivided so vertex-nearest is ~0.5 mm accurate; nricp has no rtree here)
tm = trimesh.Trimesh(H['v'].copy(), H['f'].copy(), process=False)
sv, sf = trimesh.remesh.subdivide(tm.vertices, tm.faces)
tgt = trimesh.Trimesh(sv, sf, process=False)
src = trimesh.Trimesh(Wv.copy(), wf, process=False)
thr = 0.02 / src.scale
steps = [[0.05 * ws_scale, 10, 0.5, 10], [0.03 * ws_scale, 10, 0.5, 10], [0.02 * ws_scale, 10, 0.5, 10], [0.01 * ws_scale, 10, 0.5, 10]]
pidx = np.where(pin)[0]
t0 = time.time()
wrap = trimesh.registration.nricp_amberg(src, tgt, source_landmarks=pidx, target_positions=Wv[pidx].copy(), steps=steps, eps=1e-5, distance_threshold=thr, use_faces=False, use_vertex_normals=True)
print('nricp s', round(time.time() - t0))
d = wrap - Wv

# ---- harmonic fill of the displacement on the follow set (uniform Laplacian, Dirichlet from skin outside)
fi = np.where(follow)[0]; bi = np.where(~follow)[0]
L = sp.diags(np.asarray(adj.sum(1)).ravel()) - adj
Lff = L[fi][:, fi].tocsc(); Lfb = L[fi][:, bi]
dn = d.copy()
for j in range(3): dn[fi, j] = spl.spsolve(Lff, -Lfb @ d[bi, j])
if alpha > 0:
    for sd in ('L', 'R'):
        em = growset(W(t.S('eyelid_ring_' + sd)), 3)  # lid flap + ring of this eye
        dn[em] = (1 - alpha) * dn[em] + alpha * dn[em].mean(0)
final = Wv + dn
# neck / no-target region: stays on the slider result
final[notarget | neck] = Wv[notarget | neck]
newP = final[w_of]  # expand to raw (seam groups share welded position -> stay tied)
assert np.abs(newP[t.rep] - newP).max() < 1e-12

surf = Surf(H['v'], H['f'], 1_200_000)
exc = np.zeros(len(t.basis), bool)
for k in ('eye_socket', 'mouth_interior', 'nostril'): exc[t.S(k)] = True
face = (t.rw == 1.0) & (t.basis[:, 2] > zc + 0.01); craw = core[w_of]; fitset = face & ~exc & ~craw  # fitset = skin without eye/mouth/nostril/lid/lip core
def st(P):
    a, b = p2s_stats(P, surf, face); c, d_ = p2s_stats(P, surf, fitset); return dict(face_rms=a, face_p95=b, fitset_rms=c, fitset_p95=d_)
stats = {'basis': st(t.basis), 'sliders': st(Ps), 'wrap': st(newP)}
print(json.dumps(stats, indent=1))
# ---- delta transfer (all 149 keys), per brief k_new = k_old + (basis_new - basis_old)
dB = newP - t.basis
nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{rnd}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{rnd}_keys.npy', nk)
np.savez(FIT + f'wrap_r{rnd}_mesh.npz', v=newP, f=t.faces)
chk = run_checks(t, nk.astype(np.float64), f'round {rnd} ws_scale={ws_scale} grow={grow} pin_follow={pin_follow}')
json.dump(dict(round=rnd, ws_scale=ws_scale, grow=grow, pin_follow=pin_follow, stats=stats, checks=chk), open(FIT + f'wrap_r{rnd}_report.json', 'w'), indent=1, default=float)
