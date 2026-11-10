"""Perturb the template SKULL (not the face) to make a guide head that differs from the template, for the circularity test (mv2).
usage (trellis2 venv python): python mv_perturb_template.py <template.npz> <out_mesh.npz> [--length 0.10] [--width -0.06] [--crown 0.0]
Head vertices only. Weights: w_back = smoothstep of y behind the ear plane (face keeps its depth), w_skull = smoothstep of z from the brow line up (face and jaw keep their width).
  y' = y + length * L * w_back        (L = head depth, nose to occiput; +0.10 = occiput 10 % further back: a longer head)
  x' = x * (1 + width * w_skull)      (-0.06 = 6 % narrower above the brows, ears included)
  z' = z + crown * H * w_crown        (crown height change, w_crown = smoothstep over the top 30 % of the head)
Writes (v, f) in the template frame (what mv_depth_views.py takes) and a json with the applied numbers. Ground truth for the test: the perturbed mesh itself."""
import sys, json, argparse, numpy as np
def ss(a, b, x): t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
ap = argparse.ArgumentParser(); ap.add_argument('tpl'); ap.add_argument('out'); ap.add_argument('--length', type=float, default=0.10); ap.add_argument('--width', type=float, default=-0.06); ap.add_argument('--crown', type=float, default=0.0); a = ap.parse_args()
d = np.load(a.tpl, allow_pickle=True); v = d['head/basis'].astype(np.float64); f = d['head/faces'].astype(np.int64)
names = [str(x) for x in d['tpl/landmark_names']]; idx = d['tpl/landmark_idx'].astype(int); lm = lambda n: v[idx[names.index(n)]]
ztop = v[:, 2].max(); chin = lm('chin_tip'); brow = lm('brow_center'); L = v[:, 1].max() - v[:, 1].min(); H = ztop - chin[2]
ear_y = np.array([v[np.asarray(d['tpl/' + k])].mean(0)[1] for k in ('ear_L', 'ear_R')]).mean()
w_back = ss(ear_y - 0.015, ear_y + 0.03, v[:, 1]) * (v[:, 2] > chin[2] + 0.03); w_skull = ss(brow[2] - 0.01, brow[2] + 0.05, v[:, 2]); w_crown = ss(ztop - 0.3 * H, ztop, v[:, 2])
w = v.copy(); w[:, 1] += a.length * L * w_back; w[:, 0] *= 1 + a.width * w_skull; w[:, 2] += a.crown * H * w_crown
np.savez(a.out, v=w, f=f); n = lambda q: (q[:, 1].max() - q[:, 1].min(), q[:, 0].max() - q[:, 0].min(), q[:, 2].max() - q[:, 2].min())
rep = dict(args=vars(a), depth_before_after_mm=[round(n(v)[0] * 1e3, 1), round(n(w)[0] * 1e3, 1)], width_before_after_mm=[round(n(v)[1] * 1e3, 1), round(n(w)[1] * 1e3, 1)], height_before_after_mm=[round(n(v)[2] * 1e3, 1), round(n(w)[2] * 1e3, 1)],
           max_move_mm=round(float(np.linalg.norm(w - v, axis=1).max() * 1e3), 1)); print(json.dumps(rep)); json.dump(rep, open(a.out.replace('.npz', '.json'), 'w'), indent=1)
