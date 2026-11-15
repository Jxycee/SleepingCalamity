# Builds the hd_mix --trim list for round 4 from a built variant: (1) tails below the jaw, (2) forward fringe, (3) the existing trims.
#   <trellis python> sweep_trim_cards.py <variant_dir> <ctx.json> [existing="2:0.6,29:0.6,31:0.6"] [tail_below=0.10] [tail_to=0.085] [fringe_mm=10] [fringe_frac=0.75]
# Tails: cards whose lowest fitted vertex is below eye_z - tail_below are cut where they first reach eye_z - tail_to (arc fraction = uv v of the vertices),
# +-0.01 m from a hash of the cid so the tips do not line up. Fringe: cards in FRONT of the skull centre (y < yc, face looks -y), z > 1.60, with vertices
# more than fringe_mm outside the reference SIDE silhouette (same test as sweep_jut_cards.py) are cut to fringe_frac. Smaller fraction wins.
import sys, os, json, zlib, numpy as np, cv2
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'hairdeck'))
from hd_sil import load_cal, load_mesh   # noqa: E402
vd, ctxp = sys.argv[1:3]; kw = dict(a.split('=', 1) for a in sys.argv[3:])
ex = kw.get('existing', '2:0.6,29:0.6,31:0.6'); below = float(kw.get('tail_below', 0.10)); to = float(kw.get('tail_to', 0.085))
fmm = float(kw.get('fringe_mm', 10)); ffr = float(kw.get('fringe_frac', 0.75))
ctx = json.load(open(ctxp)); cal, M = load_cal(ctx['hull']); ez = cal['eye_z']
S = np.load(os.path.join(vd, 'strands.npz')); cid, vv = S['cid'], S['uv'][:, 1]; HV = np.load(os.path.join(vd, 'opt', 'fit_best.npz'))['v'].astype(float)
hv, _ = load_mesh(ctx['head'], 'AvatarHead'); yc = hv[hv[:, 2] > 1.6][:, 1].mean()
c = cal['side']; s = c['s']; sz = c.get('sz', s)
px = c['cx'] + c['sign'] * s * HV[:, 1]; py = c['eye_py'] - sz * (HV[:, 2] - ez)
dt = cv2.distanceTransform((~M['side']).astype(np.uint8), cv2.DIST_L2, 5); h, w = dt.shape
xi = np.clip(np.round(px).astype(int), 0, w - 1); yi = np.clip(np.round(py).astype(int), 0, h - 1)
d = dt[yi, xi] + np.abs(px - xi) + np.abs(py - yi); d = np.where(M['side'][yi, xi] & (np.abs(px - xi) < 1) & (np.abs(py - yi) < 1), 0, d)
mm = d / min(s, sz) * 1000
trim = {int(k.split(':')[0]): float(k.split(':')[1]) for k in ex.split(',') if k}; tails, fringe = {}, {}
for k in np.unique(cid):
    m = cid == k
    if HV[m, 2].min() < ez - below:
        lo = m & (HV[:, 2] < ez - to); f = float(vv[lo].min()) if lo.any() else 1.0
        f = float(np.clip(f + ((zlib.crc32(b'cid%d' % k) % 2001) - 1000) / 1000 * 0.01 / max(1e-3, np.ptp(HV[m, 2])), 0.2, 1.0)); tails[int(k)] = round(f, 3)
    if (m & (HV[:, 2] > 1.60) & (HV[:, 1] < yc) & (mm > fmm)).any(): fringe[int(k)] = ffr
for src in (tails, fringe):
    for k, f in src.items(): trim[k] = min(trim.get(k, 1.0), f)
print('TAILS', len(tails), sorted(tails.items())); print('FRINGE', len(fringe), sorted(fringe)); print('ez', round(ez, 4), 'yc', round(yc, 3))
print('TRIM', ','.join('%d:%g' % (k, f) for k, f in sorted(trim.items())))
