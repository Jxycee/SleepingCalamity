"""Hybrid head prototype: skull / back / ears / neck from the multiview fit (B), face region from the single-photo slider fit (A). Both are template-topology basis meshes
(<run>/fit/wrap_rh6_basis.npy). usage: python hybrid_face.py <template.npz> <A_basis.npy> <B_basis.npy> <out_basis.npy> [--fade 14] 
Steps: face region = front-facing verts between chin and brow (template geometry); A is similarity-aligned (Umeyama) onto B over the nose/eye/mouth core; then
v = B + w (A' - B) with w = 1 in the face, smoothstep fade over --fade mm; welded duplicates take the max w (seams stay closed). Prints per-region drift. No stage is changed."""
import sys, numpy as np
from scipy.spatial import cKDTree
tpl, pa, pb, out = sys.argv[1:5]; fade = float(sys.argv[sys.argv.index('--fade') + 1]) if '--fade' in sys.argv else 14.0
z = np.load(tpl, allow_pickle=True); T = z['head/basis'].astype(np.float64); F = z['head/faces'].astype(int)
A = np.load(pa).astype(np.float64); B = np.load(pb).astype(np.float64)
names = [str(x) for x in z['tpl/landmark_names']]; idx = z['tpl/landmark_idx'].astype(int); L = lambda n: T[idx[names.index(n)]]
chin = L('chin_tip')[2]
tri = T[F]; nrm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); vn = np.zeros_like(T)
for k in range(3): np.add.at(vn, F[:, k], nrm)
vn /= np.linalg.norm(vn, axis=1)[:, None] + 1e-12
eye_z = np.mean([L(n)[2] for n in names if 'eye' in n and 'corner' in n] or [chin + 0.12])
face = (vn[:, 1] < -0.15) & (T[:, 2] > chin - 0.012) & (T[:, 2] < eye_z + 0.05) & (np.abs(T[:, 0]) < 0.072)
core = face & (T[:, 2] < eye_z + 0.02) & (np.abs(T[:, 0]) < 0.05)
def umeyama(X, Y):
    mx, my = X.mean(0), Y.mean(0); Xc, Yc = X - mx, Y - my; U, S, Vt = np.linalg.svd(Yc.T @ Xc / len(X)); D = np.eye(3); D[2, 2] = np.sign(np.linalg.det(U @ Vt))
    R = U @ D @ Vt; s = (S * np.diag(D)).sum() / Xc.var(0).sum(); return s, R, my - s * R @ mx
s, R, t = umeyama(A[core], B[core]); A2 = s * A @ R.T + t
print('similarity A->B on face core: scale %.4f, residual rms %.2f mm' % (s, np.sqrt(((A2[core] - B[core]) ** 2).sum(1).mean()) * 1e3))
if '--ears' in sys.argv:   # also take the (clean, template-shaped) ears from A: the multiview wrap crumples them
    ear = np.zeros(len(T), bool); ear[z['tpl/ear_L'].astype(int)] = True; ear[z['tpl/ear_R'].astype(int)] = True; ear = np.isin(z['tpl/weld_rep'].astype(int), z['tpl/weld_rep'].astype(int)[ear]); face = face | ear
d = cKDTree(T[face]).query(T)[0] * 1e3; w = np.clip(1 - d / fade, 0, 1); w = w * w * (3 - 2 * w); w[face] = 1.0
rep = z['tpl/weld_rep'].astype(int); wm = np.zeros(len(w)); np.maximum.at(wm, rep, w); w = wm[rep]
H = B + w[:, None] * (A2 - B)
H = H.astype(np.float32).astype(np.float64); assert np.abs(H[rep] - H).max() < 2e-3, 'weld seams opened'
print('face verts %d, blended %d; mean move of face verts %.2f mm (max %.2f)' % (face.sum(), (w > 0).sum(), np.linalg.norm(H - B, axis=1)[face].mean() * 1e3, np.linalg.norm(H - B, axis=1).max() * 1e3))
fn = lambda P: np.cross(P[F][:, 1] - P[F][:, 0], P[F][:, 2] - P[F][:, 0]); fl = ((fn(H) * fn(T)).sum(1) < 0)
print('flipped faces vs template: B %d, A %d, hybrid %d' % (((fn(B) * fn(T)).sum(1) < 0).sum(), ((fn(A2) * fn(T)).sum(1) < 0).sum(), fl.sum()))
np.save(out, H.astype(np.float32)); np.save(out.replace('.npy', '_mask.npy'), w.astype(np.float32))
