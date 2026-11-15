# List the cards (hd_strands cid) with fitted verts > mm outside the reference SIDE silhouette, z > 1.60, behind the skull centre (face looks -y).
#   <trellis python> sweep_jut_cards.py <variant_dir> <ctx.json> [mm=15] [keep_over_mm=20 when more than max_cards qualify] [max_cards=15]
import sys, os, json, numpy as np, cv2
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'hairdeck'))
from hd_sil import load_cal, load_mesh, AX   # noqa: E402
vd, ctxp = sys.argv[1:3]; mm0 = float(sys.argv[3]) if len(sys.argv) > 3 else 15.0; mm1 = float(sys.argv[4]) if len(sys.argv) > 4 else 20.0
mx = int(sys.argv[5]) if len(sys.argv) > 5 else 15
ctx = json.load(open(ctxp)); cal, M = load_cal(ctx['hull'])
HV = np.load(os.path.join(vd, 'opt', 'fit_best.npz'))['v'].astype(float); cid = np.load(os.path.join(vd, 'strands.npz'))['cid']
hv, _ = load_mesh(ctx['head'], 'AvatarHead'); yc = hv[hv[:, 2] > 1.6][:, 1].mean()
c = cal['side']; s = c['s']; sz = c.get('sz', s)
px = c['cx'] + c['sign'] * s * HV[:, 1]; py = c['eye_py'] - sz * (HV[:, 2] - cal['eye_z'])
dt = cv2.distanceTransform((~M['side']).astype(np.uint8), cv2.DIST_L2, 5); h, w = dt.shape
xi = np.clip(np.round(px).astype(int), 0, w - 1); yi = np.clip(np.round(py).astype(int), 0, h - 1)
d = dt[yi, xi] + np.abs(px - xi) + np.abs(py - yi); d = np.where(M['side'][yi, xi] & (np.abs(px - xi) < 1) & (np.abs(py - yi) < 1), 0, d)
mm = d / min(s, sz) * 1000; ok = (HV[:, 2] > 1.60) & (HV[:, 1] > yc)
worst = {int(k): float(mm[ok & (cid == k)].max()) for k in np.unique(cid[ok & (mm > mm0)])}
if len(worst) > mx: worst = {k: v for k, v in worst.items() if v > mm1}
print('skull y', round(yc, 3), 'overall max', round(float(mm[HV[:, 2] > 1.6].max()), 1), 'cards', len(worst))
print('J', ','.join(str(k) for k in sorted(worst))); print({k: round(v, 1) for k, v in sorted(worst.items(), key=lambda t: -t[1])})
