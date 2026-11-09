"""Round 4 global step (replaces local skull/silhouette pairs): ONE height stretch of the skull above the brow line (smoothstep, 0 at/below the brow) and the template's own
Head_Width_Up / Head_Cheek_Out key deltas, amounts solved from the photo silhouette (row-centred half widths, eye-aligned similarity; photo yaw cancelled by centring each row).
usage: r4_02_global.py <in_basis.npy> <render.png> <cam.json> <lm.json> <tag> [--noheight] [--nowidth] [--hmax 20] [--amax 2.0]"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import json, argparse
from PIL import Image
from scipy import ndimage as ndi
from scipy.optimize import least_squares
from r4_common import *
from fit_checks6 import run_checks   # the documented gate set (was the stricter round-3 fit_checks: gap mean +0.3, penetration +0.25)
import r4_metrics as MT
ap = argparse.ArgumentParser(); [ap.add_argument(a) for a in ('inb', 'png', 'camj', 'lmj', 'tag')]
ap.add_argument('--noheight', action='store_true'); ap.add_argument('--nowidth', action='store_true'); ap.add_argument('--hmax', type=float, default=20.0); ap.add_argument('--amax', type=float, default=2.0)
ap.add_argument('--fixh', type=float, default=None, help='force height stretch (mm) instead of solving'); ap.add_argument('--fixw', default=None, help='force aw,ac')
A = ap.parse_args()
t = Tpl(); P0 = np.load(A.inb).astype(np.float64); cam = json.load(open(A.camj)); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
rl = json.load(open(A.lmj)); rp = np.array([rl[str(i)] for i in range(468)]); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:468, :2]
EC = [33, 263, 133, 362]; s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True); Rr2 = Rr[:2, :2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
mmpp = S_ / R_    # mm(m) per render px
mv = np.array(Image.open(TGT + 'mask_vis.png').convert('RGB')).astype(int); head = (mv[..., 0] - mv[..., 1]) <= 50
head = ndi.binary_opening(head, iterations=2); lab, n = ndi.label(head); head = ndi.binary_fill_holes(lab == lab[int(ph[168, 1]), int(ph[168, 0])])
ys = np.where(head.any(1))[0]; ytop = ys.min()
rim = np.array(Image.open(A.png).convert('RGB')).astype(int); rm = ndi.binary_opening(np.abs(rim - rim[2, 2]).sum(2) > 25, iterations=2)
rrows = np.where(rm.any(1))[0]; rtop = rrows.min()
# top: photo top point -> render px -> z
xm = np.where(head[ytop + 3])[0].mean(); u = inv(np.array([[xm, ytop]]))[0]; z_ph_top = cz + (0.5 - u[1] / R_) * S_; z_r_top = cz + (0.5 - rtop / R_) * S_
dz_top = z_ph_top - z_r_top; print('top of head: photo-render dz %.1f mm' % (dz_top * 1e3))
zb = t.basis[t.lm_idx[t.lm_names.index('brow_center')], 2]; ztop = P0[:, 2].max()
prof = lambda z: (lambda x: x * x * (3 - 2 * x))(np.clip((z - zb) / (ztop - zb), 0, 1))
h = (A.fixh * 1e-3 if A.fixh is not None else float(np.clip(dz_top, -A.hmax * 1e-3, A.hmax * 1e-3))) if not A.noheight else 0.0
P1 = P0.copy(); P1[:, 2] += h * prof(P0[:, 2]); print('height stretch %.1f mm' % (h * 1e3))
# width deficits per row (render half-width vs photo half-width, both in metres)
ymouth = ph[[61, 291], 1].mean(); rows = np.arange(ytop + 25, int(ymouth), 6); Z, DEF = [], []
for yp in rows:
    rb = np.where(head[yp])[0]
    if len(rb) < 5: continue
    xp = (rb.min() + rb.max()) / 2; uu = inv(np.array([[xp, yp]]))[0]; vr = int(round(uu[1]))
    if vr < 0 or vr >= rm.shape[0]: continue
    rr = np.where(rm[vr])[0]
    if len(rr) < 5: continue
    hp = (rb.max() - rb.min()) / 2 / s2 * mmpp; hr = (rr.max() - rr.min()) / 2 * mmpp
    Z.append(cz + (0.5 - vr / R_) * S_ + h * float(prof(cz + (0.5 - vr / R_) * S_)) * 0); DEF.append(hp - hr)
Z = np.array(Z); DEF = np.array(DEF)
kw = t.keys[t.kidx('Head_Width_Up')] - t.basis; kc = t.keys[t.kidx('Head_Cheek_Out')] - t.basis
def width_of(P, z, dz=0.004):
    m = np.abs(P[:, 2] - z) < dz; return np.abs(P[m, 0]).max() if m.any() else np.nan
def model(a):
    Pw = P1 + a[0] * kw + a[1] * kc
    return np.array([width_of(Pw, z) - width_of(P1, z) for z in Z])
if not A.nowidth:
    if A.fixw: a = np.array([float(v) for v in A.fixw.split(',')])
    else:
        res = least_squares(lambda a: np.nan_to_num(model(a) - DEF) + 0.02 * np.r_[a, 0][:1].sum() * 0, [0.0, 0.0], bounds=([0, 0], [A.amax, A.amax]), diff_step=0.05)
        a = res.x
    print('row deficits mm (photo-render half width): mean %.2f  min %.2f max %.2f' % (DEF.mean() * 1e3, DEF.min() * 1e3, DEF.max() * 1e3))
    print('Head_Width_Up %.2f  Head_Cheek_Out %.2f ; residual rms mm %.2f -> %.2f' % (a[0], a[1], np.sqrt(np.nanmean(DEF ** 2)) * 1e3, np.sqrt(np.nanmean((DEF - model(a)) ** 2)) * 1e3))
else: a = np.zeros(2)
d = a[0] * kw + a[1] * kc
for side in ('L', 'R'):   # eyes rigid
    ring = t.S('eyelid_ring_' + side).astype(int); m = d[ring].mean(0); dd = cKDTree(P1[ring]).query(P1)[0]; x = np.clip((dd - 0.006) / 0.010, 0, 1); w = (1 - x * x * (3 - 2 * x)) * ((P1[:, 0] > 0) == (side == 'L'))
    d = w[:, None] * m + (1 - w[:, None]) * d
newP = P1 + d
from fit_common import repair_flips
newP, _f0, _f1 = repair_flips(t, newP); print('global flips before/after repair', _f0, _f1)
def fn(P): tri = P[t.faces]; n = np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0]); return n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30)
flips = int(((fn(newP) * fn(t.basis)).sum(1) < 0).sum()); print('flips', flips)
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{A.tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{A.tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.tag}_mesh.npz', v=newP, f=t.faces)
print('3D target', MT.tgt_stats(newP)); chk = run_checks(t, nk.astype(np.float64), f'global {A.tag}', verbose=False); print('GATES PASS=%s' % chk['PASS'], chk.get('fails'))
json.dump(dict(args=vars(A), height_mm=h * 1e3, width_up=a[0], cheek_out=a[1], flips=flips, checks=chk), open(FIT + f'wrap_r{A.tag}_report.json', 'w'), indent=1, default=float)
