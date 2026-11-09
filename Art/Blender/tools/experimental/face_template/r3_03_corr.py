"""Round 3 closed-loop front-plane correction (after the nricp wrap).
Render the current head front-ortho -> mediapipe landmarks (same detector as on the photo) -> similarity from the 4 eye corners (eye-aligned) ->
per-landmark + head-silhouette (skull top/sides above the brows, from the photo mask) displacement pairs in mm (x,z) -> thin-plate field in (x,z) applied to every vertex
(extruded along y) -> eyes stay rigid (ring translation), pins fade out under the chin -> delta-transfer all keys -> gates.
usage: r3_03_corr.py <in_basis.npy> <render_lm.json> <cam.json> <render.png> <tag> [--smooth 30] [--sil 1] [--cap 30] [--fadelo 45]"""
import json, sys, argparse
from PIL import Image
from scipy.interpolate import RBFInterpolator
from scipy import ndimage as ndi
from r3_common import *
from fit_checks import run_checks
ap = argparse.ArgumentParser(); ap.add_argument('inb'); ap.add_argument('lmj'); ap.add_argument('camj'); ap.add_argument('png'); ap.add_argument('tag')
ap.add_argument('--smooth', type=float, default=30.0); ap.add_argument('--sil', type=int, default=1); ap.add_argument('--cap', type=float, default=30.0)
ap.add_argument('--fadelo', type=float, default=45.0); ap.add_argument('--ex', default='', help='extra excluded landmark ids')
ap.add_argument('--rowsbelow', type=float, default=-50.0, help='silhouette side rows run to brow line + this (photo px)')
ap.add_argument('--boost', type=float, default=0.0, help='smoothing for lip/brow/nose/chin landmark pairs (0 = same as --smooth)')
ap.add_argument('--eyer', default='6,16', help='rigid eye zone radii mm (full rigid inside r1, fades to the field at r2)')
ap.add_argument('--damp', type=float, default=1.0, help='fraction of the measured displacement applied (iteration damping)')
A = ap.parse_args()
t = Tpl(); P0 = np.load(A.inb).astype(np.float64); cam = json.load(open(A.camj)); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
chin_z = t.basis[t.lm_idx[t.lm_names.index('chin_tip')]][2]
rl = json.load(open(A.lmj)); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:, :2]
rp = np.array([rl[str(i)] for i in range(468)])
px2mm = lambda p: np.stack([cx + (p[:, 0] / R_ - 0.5) * S_, cz + (0.5 - p[:, 1] / R_) * S_], 1)   # render px -> (x,z) in m
# --- eye-aligned similarity render px -> photo px (4 eye corners)
EC = [33, 263, 133, 362]
s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True)
Rr2 = Rr[:2, :2]; sim = lambda p: s2 * p @ Rr2.T + tt[:2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
print('similarity scale photo px per render px %.4f rot %.2f deg' % (s2, np.degrees(np.arctan2(Rr2[1, 0], Rr2[0, 0]))))
RING = [33,7,163,144,145,153,154,155,133,173,157,158,159,160,161,246, 362,382,381,380,374,373,390,249,263,466,388,387,386,385,384,398]
excl = set(RING) | {int(x) for x in A.ex.split(',') if x}
ids = [i for i in range(468) if i not in excl]
src_mm = px2mm(rp[ids]); tgt_mm = px2mm(inv(ph[ids]))
d = tgt_mm - src_mm; mag = np.linalg.norm(d, axis=1) * 1e3
ok = mag < A.cap; print('landmark pairs', ok.sum(), 'of', len(ids), 'dropped (>cap mm):', [ids[i] for i in np.where(~ok)[0]][:20], 'median |d| mm %.2f p95 %.2f' % (np.median(mag), np.percentile(mag, 95)))
S_list = [src_mm[ok]]; T_list = [tgt_mm[ok]]
# --- silhouette pairs: skull top and sides above the brow line
if A.sil > 0:
    mv = np.array(Image.open(TGT + 'mask_vis.png').convert('RGB')).astype(int)
    bgpix = (mv[..., 0] - mv[..., 1]) > 50       # red tint marks the background
    head = ~bgpix; head = ndi.binary_opening(head, iterations=2); lab, n = ndi.label(head); head = lab == lab[int(ph[168, 1]), int(ph[168, 0])]
    head = ndi.binary_fill_holes(head)
    ys, xs = np.where(head); ytop = ys.min()
    rim = np.array(Image.open(A.png).convert('RGB')).astype(int); bg = rim[2, 2]; rm = ndi.binary_opening((np.abs(rim - bg).sum(2) > 25), iterations=2)
    ybrow = ph[[105, 334], 1].mean()
    sp_, tp_ = [], []
    for yp in np.arange(ytop + 30, ybrow + A.rowsbelow, 14):
        row = np.where(head[int(yp)])[0]
        if len(row) < 5: continue
        hp = (row.max() - row.min()) / 2.0          # photo half-width; the target is symmetric about the wrap's own midline (the photo is yawed -2.7 deg)
        for xp in (row.min(), row.max()):
            u = inv(np.array([[xp, yp]]))[0]
            vr = int(round(u[1])); r = np.where(rm[vr])[0]
            if len(r) < 5: continue
            mid = (r.min() + r.max()) / 2.0; left = xp < (row.min() + row.max()) / 2
            xr = r.min() if left else r.max()
            sp_.append([xr, vr]); tp_.append([mid + (-1 if left else 1) * hp / s2, u[1]])
    nside = len(sp_)
    xl, xr_ = np.where(head[ytop + 40])[0][[0, -1]]
    for xp in np.linspace(xl + 0.12 * (xr_ - xl), xr_ - 0.12 * (xr_ - xl), 15):
        col = np.where(head[:, int(xp)])[0]; u = inv(np.array([[xp, col.min()]]))[0]
        c = int(round(u[0])); r = np.where(rm[:, c])[0]
        if len(r) < 5: continue
        sp_.append([c, r.min()]); tp_.append(u)
    sp_, tp_ = np.array(sp_, float), np.array(tp_, float); dd = px2mm(tp_) - px2mm(sp_)
    print('silhouette pairs', len(sp_), '(sides %d) mean dx side L/R mm %.1f/%.1f ; top dz mm %.1f' % (nside, dd[:nside][::2, 0].mean() * 1e3, dd[:nside][1::2, 0].mean() * 1e3, dd[nside:, 1].mean() * 1e3))
    for _ in range(A.sil): S_list.append(px2mm(sp_)); T_list.append(px2mm(tp_))
Sx = np.concatenate(S_list) * 1e3; Tx = np.concatenate(T_list) * 1e3
key = np.round(Sx, 3); _, inv_i = np.unique(key, axis=0, return_inverse=True); inv_i = inv_i.ravel()
Su = np.zeros((inv_i.max() + 1, 2)); Tu = np.zeros_like(Su); cnt = np.bincount(inv_i)
np.add.at(Su, inv_i, Sx); np.add.at(Tu, inv_i, Tx); Su /= cnt[:, None]; Tu /= cnt[:, None]
BOOST = set([0,267,269,270,409,291,375,321,405,314,17,84,181,91,146,61,185,40,39,37,78,191,80,81,82,13,312,311,310,415,308,324,318,402,317,14,87,178,88,95,
             70,63,105,66,107,55,65,52,53,46,300,293,334,296,336,285,295,282,283,276, 1,2,4,5,6,19,94,98,327,129,358,168,195,197,48,278,64,294,97,326,
             152,148,176,149,150,377,400,378,379,172,397,136,365,58,288,132,361])
sm = np.full(len(Su), A.smooth)
if A.boost > 0:
    bid = np.zeros(len(Sx), bool); bid[:len(ids)] = False
    lm_pts = src_mm[ok] * 1e3; lm_ids_ok = [ids[i] for i in np.where(ok)[0]]
    bpts = np.array([lm_pts[i] for i, k in enumerate(lm_ids_ok) if k in BOOST])
    kd_b = cKDTree(bpts); sm[kd_b.query(Su)[0] < 1e-3] = A.boost
    print('boosted points', int((sm < A.smooth).sum()), 'smoothing', A.boost, 'vs', A.smooth)
rb = RBFInterpolator(Su, Tu - Su, kernel='thin_plate_spline', smoothing=sm, degree=1)
xz = P0[:, [0, 2]] * 1e3; dxz = rb(xz) * 1e-3 * A.damp
ft = np.clip((P0[:, 2] - (chin_z - A.fadelo * 1e-3)) / (A.fadelo * 1e-3 + 0.005), 0, 1); ft = ft * ft * (3 - 2 * ft)
dw = np.zeros_like(P0); dw[:, 0] = dxz[:, 0] * ft; dw[:, 2] = dxz[:, 1] * ft
EY1, EY2 = [float(v) * 1e-3 for v in A.eyer.split(',')]
for side in ('L', 'R'):
    ring = t.S('eyelid_ring_' + side).astype(int); m = dw[ring].mean(0)
    dist = cKDTree(P0[ring]).query(P0)[0]; x = np.clip((dist - EY1) / (EY2 - EY1), 0, 1); wgt = 1 - x * x * (3 - 2 * x)
    wgt = wgt * ((P0[:, 0] > 0) == (side == 'L'))
    dw = wgt[:, None] * m + (1 - wgt[:, None]) * dw
newP = P0 + dw
print('field |d| mm: mean %.2f max %.2f ; top of head dz %.1f' % (np.linalg.norm(dw, axis=1).mean() * 1e3, np.linalg.norm(dw, axis=1).max() * 1e3, dw[np.argmax(P0[:, 2]), 2] * 1e3))
def fn(P): tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
flips = int(((fn(newP) * fn(t.basis)).sum(1) < 0).sum()); print('whole-head rest flips vs template', flips)
assert np.abs(newP[t.rep] - newP).max() < 1e-4, 'weld seams split'
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{A.tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{A.tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.tag}_mesh.npz', v=newP, f=t.faces)
chk = run_checks(t, nk.astype(np.float64), f'corr {A.tag}', verbose=False)
print('GATES PASS=%s' % chk['PASS'])
for k, v in chk.items():
    if isinstance(v, dict): print(' ', k, json.dumps({a: (round(float(b), 3) if not isinstance(b, dict) else {x: round(float(y), 2) for x, y in b.items()}) for a, b in v.items()}))
json.dump(dict(args=vars(A), flips=flips, checks=chk), open(FIT + f'wrap_r{A.tag}_report.json', 'w'), indent=1, default=float)
