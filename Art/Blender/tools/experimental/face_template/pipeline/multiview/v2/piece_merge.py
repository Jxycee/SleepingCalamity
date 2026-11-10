"""Per-piece merge prototype (template topology, index correspondence): each piece comes from its own source and is fitted to the skull source on its own.
  skull / back / neck  = B (multiview fit, ideally with the lip patch left unmoved by r7)
  face (+ lip patch)   = A (single-photo slider fit), one similarity (Umeyama) on the nose/eye/mouth core
  ears                 = donor ears from A, ONE similarity PER EAR onto B's ear (index pairs) so size / pose / position follow the skull source
Blend: weight 1 on the piece, smoothstep fade (--fade mm face, --earfade mm ears), welded twins share the max weight; faces that flip against the template are
relaxed back toward B (weights halved up to 8 rounds). usage: piece_merge.py <template.npz> <A.npy> <B.npy> <out.npy> [--fade 14] [--earfade 8] [--noears] [--noface]"""
import sys, numpy as np
from scipy.spatial import cKDTree
tpl, pa, pb, out = sys.argv[1:5]
opt = lambda k, d: float(sys.argv[sys.argv.index(k) + 1]) if k in sys.argv else d
fade, earfade = opt('--fade', 14.0), opt('--earfade', 8.0)
z = np.load(tpl, allow_pickle=True); T = z['head/basis'].astype(np.float64); F = z['head/faces'].astype(int); g = lambda k: z['tpl/' + k].astype(int)
A = np.load(pa).astype(np.float64); B = np.load(pb).astype(np.float64); rep = g('weld_rep')
names = [str(x) for x in z['tpl/landmark_names']]; idx = g('landmark_idx'); L = lambda n: T[idx[names.index(n)]]
chin = L('chin_tip')[2]; eye_z = np.mean([L(n)[2] for n in names if 'eye' in n and 'corner' in n] or [chin + 0.12])
tri = T[F]; nrm = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); vn = np.zeros_like(T)
for k in range(3): np.add.at(vn, F[:, k], nrm)
vn /= np.linalg.norm(vn, axis=1)[:, None] + 1e-12
twin = lambda m: np.isin(rep, rep[m])
face = (vn[:, 1] < -0.15) & (T[:, 2] > chin - 0.012) & (T[:, 2] < eye_z + 0.05) & (np.abs(T[:, 0]) < 0.072)
lp = np.zeros(len(T), bool); lp[np.concatenate([g('lip_patch_upper_verts'), g('lip_patch_lower_verts')])] = True; face |= twin(lp)    # lip patch always travels with the face
earL = np.zeros(len(T), bool); earL[g('ear_L')] = True; earL = twin(earL); earR = np.zeros(len(T), bool); earR[g('ear_R')] = True; earR = twin(earR)
core = face & (T[:, 2] < eye_z + 0.02) & (np.abs(T[:, 0]) < 0.05)

def umeyama(X, Y):
    mx, my = X.mean(0), Y.mean(0); Xc, Yc = X - mx, Y - my; U, S, Vt = np.linalg.svd(Yc.T @ Xc / len(X)); D = np.eye(3); D[2, 2] = np.sign(np.linalg.det(U @ Vt))
    R = U @ D @ Vt; s = (S * np.diag(D)).sum() / Xc.var(0).sum(); return s, R, my - s * R @ mx
def apply(s, R, t, P): return s * P @ R.T + t
def fade_w(mask, mm):
    d = cKDTree(T[mask]).query(T)[0] * 1e3; w = np.clip(1 - d / mm, 0, 1); w = w * w * (3 - 2 * w); w[mask] = 1.0; return w

s, R, t = umeyama(A[core], B[core]); Af = apply(s, R, t, A); print('face: scale %.4f core rms %.2f mm' % (s, np.sqrt(((Af[core] - B[core]) ** 2).sum(1).mean()) * 1e3))
wf = fade_w(face, fade); P_face = Af
if '--noface' in sys.argv: wf[:] = 0.0   # keep the skull-source face (clean once the r7 lip-patch fix is in): ears-only merge
H = B + wf[:, None] * (P_face - B); W = wf.copy()
if '--noears' not in sys.argv:
    for nm, m in (('earL', earL), ('earR', earR)):
        se, Re, te = umeyama(A[m], B[m]); Ae = apply(se, Re, te, A); r = np.sqrt(((Ae[m] - B[m]) ** 2).sum(1).mean()) * 1e3
        print('%s: scale %.3f, residual to the skull-source ear %.2f mm (shape difference left after pose/size fit)' % (nm, se, r))
        we = fade_w(m, earfade); we = np.where(wf > we, 0.0, we); H = H + we[:, None] * (Ae - H); W = np.maximum(W, we)
fn = lambda P: np.cross(P[F][:, 1] - P[F][:, 0], P[F][:, 2] - P[F][:, 0]); flip = lambda P: (fn(P) * fn(T)).sum(1) < 0
D = H - B; k = np.ones(len(T)); H = B + D; nf0 = int(flip(H).sum())
for rnd in range(8):                       # relax the displacement of vertices touching a flipped face (welded twins together) back toward B
    bad = flip(H)
    if not bad.any(): break
    m = twin(np.isin(np.arange(len(T)), np.unique(F[bad]))); k[m] *= 0.5; H = B + k[:, None] * D
print('flips: B %d, merged before relax %d, after relax %d' % (flip(B).sum(), nf0, flip(H).sum()))
H = H.astype(np.float32).astype(np.float64); assert np.abs(H[rep] - H).max() < 2.5e-3, 'weld seams opened'
print('moves vs B: face mean %.2f mm, ears mean %.2f mm' % (np.linalg.norm(H - B, axis=1)[face].mean() * 1e3, np.linalg.norm(H - B, axis=1)[earL | earR].mean() * 1e3))
np.save(out, H.astype(np.float32))
