"""Skull width step (after the snap): the closed-loop fit only matches landmark ratios, so the dome above the brows keeps the template's width (char2: render 14 % wider than the
photo at the crown, skull IoU 0.93). Row by row, the photo head half-width (row-centred, eye-aligned similarity, as r4_02_global) is compared with the render's; a smooth horizontal
scale  x *= 1 + a1*p(z) + a2*p(z)^3  (p = smoothstep from the brow line to the top of the head, 0 at and below the brow, so eyes, brows and every ratio landmark are untouched) is
solved in least squares (signed, bounded) and applied to the wrap basis and all keys (delta transfer), then written back over wrap_r<tag>_*.
usage: r6_04_skullw.py <tag>   (reads fit/wrap_r<tag>_basis.npy, rn_<tag>.png, _cam_<tag>.json, lm_<tag>.json, target/landmarks.json, mask_vis.png)"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, json
from PIL import Image
from scipy import ndimage as ndi
from scipy.optimize import least_squares
from r6_common import *
from fit_common import count_flips
tag = sys.argv[1]
t = Tpl(); P0 = np.load(FIT + f'wrap_r{tag}_basis.npy').astype(np.float64); cam = json.load(open(FIT + f'_cam_{tag}.json')); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
rl = json.load(open(FIT + f'lm_{tag}.json')); rp = np.array([rl[str(i)] for i in range(468)]); ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:468, :2]
EC = [33, 263, 133, 362]; s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True); Rr2 = Rr[:2, :2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
mmpp = S_ / R_
mv = np.array(Image.open(TGT + 'mask_vis.png').convert('RGB')).astype(int); head = (mv[..., 0] - mv[..., 1]) <= 50
head = ndi.binary_opening(head, iterations=2); lab, n = ndi.label(head); head = ndi.binary_fill_holes(lab == lab[int(ph[168, 1]), int(ph[168, 0])])
ytop = np.where(head.any(1))[0].min(); ybrow = int(ph[[105, 334], 1].mean())
rim = np.array(Image.open(FIT + f'rn_{tag}.png').convert('RGB')).astype(int); rm = ndi.binary_opening(np.abs(rim - rim[2, 2]).sum(2) > 25, iterations=2)
zb = t.basis[t.lm_idx[t.lm_names.index('brow_center')], 2]; ztop = P0[:, 2].max()
prof = lambda z: (lambda x: x * x * (3 - 2 * x))(np.clip((z - zb) / (ztop - zb), 0, 1))
Z, DEF = [], []
for yp in range(ytop + 6, ybrow - 10, 4):
    rb = np.where(head[yp])[0]
    if len(rb) < 5: continue
    xp = (rb.min() + rb.max()) / 2; uu = inv(np.array([[xp, yp]]))[0]; vr = int(round(uu[1]))
    if vr < 0 or vr >= rm.shape[0]: continue
    rr = np.where(rm[vr])[0]
    if len(rr) < 5: continue
    hp = (rb.max() - rb.min()) / 2 / s2 * mmpp; hr = (rr.max() - rr.min()) / 2 * mmpp
    Z.append(cz + (0.5 - vr / R_) * S_); DEF.append(hp - hr)
Z = np.array(Z); DEF = np.array(DEF)
def width_of(P, z, dz=0.004):
    m = np.abs(P[:, 2] - z) < dz; return np.abs(P[m, 0]).max() if m.any() else np.nan
W0 = np.array([width_of(P0, z) for z in Z]); ok = np.isfinite(W0) & (prof(Z) > 0.02)
def apply(a, P):
    Q = P.copy(); p = prof(P[:, 2]); Q[:, 0] *= 1 + a[0] * p + a[1] * p ** 3; return Q
def res(a): return np.array([(width_of(apply(a, P0), z) - w0) for z, w0 in zip(Z[ok], W0[ok])]) - DEF[ok]
a = np.zeros(2)
if ok.sum() >= 4:
    sol = least_squares(lambda a: np.r_[np.nan_to_num(res(a)), 0.0005 * a], np.zeros(2), bounds=([-0.3, -0.3], [0.1, 0.1]), diff_step=0.02); a = sol.x
newP = apply(a, P0); fl = int(count_flips(t, newP).sum())
print('skull width: rows %d, deficit mean %.2f mm (photo - render half width), a=%s, residual rms %.2f -> %.2f mm, flips %d' % (ok.sum(), DEF[ok].mean() * 1e3 if ok.any() else 0, np.round(a, 3), np.sqrt(np.mean(DEF[ok] ** 2)) * 1e3 if ok.any() else 0,
      np.sqrt(np.mean(res(a) ** 2)) * 1e3 if ok.any() else 0, fl))
if fl: newP, a = P0, np.zeros(2); print('flips -> step skipped')
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=newP, f=t.faces)
json.dump(dict(a=a.tolist()), open(FIT + 'skullw.json', 'w'))
print('SKULLW DONE')
