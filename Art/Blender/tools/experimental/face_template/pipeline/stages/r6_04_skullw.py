"""Skull width step (after the snap): the closed-loop fit only matches landmark ratios, so the dome above the brows keeps the template's width (char2: render 14 % wider than the
photo at the crown, skull IoU 0.93). Row by row, the photo head half-width (row-centred, eye-aligned similarity, as r4_02_global) is compared with the render's; a smooth horizontal
scale  x *= 1 + a1*p(z) + a2*p(z)^3  (p = smoothstep from the brow line to the top of the head, 0 at and below the brow, so eyes, brows and every ratio landmark are untouched) is
solved in least squares (signed, bounded) and applied to the wrap basis and all keys (delta transfer), then written back over wrap_r<tag>_*.
Crown closure (after the width step, skipped with --nocrown, accepted only when it lowers the dome width-profile error and flips nothing): the render dome of a shaved head can be pointed or
slanted (the template cranium + a jagged / open-topped Hunyuan top). Per height z above the brow a symmetric target half-width HT(z) = the photo row half-width (smoothed) is set; vertices are re-centred on the face midline and scaled in x by HT/HR (HR = the render's own half-width at that z), weighted by the same smoothstep from the brow.
usage: r6_04_skullw.py <tag> [--nocrown]   (reads fit/wrap_r<tag>_basis.npy, rn_<tag>.png, _cam_<tag>.json, lm_<tag>.json, target/landmarks.json, mask_vis.png)"""
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
ZB0 = zb - float(_os.environ.get('SCP_PROF_LOW', '0.008'))     # the profile starts 8 mm below the brow line (round 10): the temple band just above the eyes was 3-8 % narrow in the final render
prof = lambda z: (lambda x: x * x * (3 - 2 * x))(np.clip((z - ZB0) / (ztop - ZB0), 0, 1))
Z, DEF, HPW = [], [], []
for yp in range(ytop + 6, ybrow + int(0.08 * np.linalg.norm(ph[33] - ph[263])), 4):
    rb = np.where(head[yp])[0]
    if len(rb) < 5: continue
    xp = (rb.min() + rb.max()) / 2; uu = inv(np.array([[xp, yp]]))[0]; vr = int(round(uu[1]))
    if vr < 0 or vr >= rm.shape[0]: continue
    rr = np.where(rm[vr])[0]
    if len(rr) < 5: continue
    hp = (rb.max() - rb.min()) / 2 / s2 * mmpp; hr = (rr.max() - rr.min()) / 2 * mmpp
    Z.append(cz + (0.5 - vr / R_) * S_); DEF.append(hp - hr); HPW.append(hp)
Z = np.array(Z); DEF = np.array(DEF); HPW = np.array(HPW)
def width_of(P, z, dz=0.004):
    m = np.abs(P[:, 2] - z) < dz; return (P[m, 0].max() - P[m, 0].min()) / 2 if m.any() else np.nan     # half-width of the row (the photo side is row-centred too; max|x| mixed in any lateral offset of the mesh)
W0 = np.array([width_of(P0, z) for z in Z]); ok = np.isfinite(W0) & (prof(Z) > 0.02)
# vertical crown alignment (round 10): the render's crown top is compared with the photo mask's top in the eye-aligned similarity; the dome above the brow is lowered / raised by the difference
# (smoothstep profile, 0 at the brow line, full at the top: every landmark ratio stays put). A crown 1-2 mm too high made the head football-shaped (width at a fixed fraction of the height drops 5-10 %).
_xp = np.where(head[ytop + 3])[0].mean(); _vt = inv(np.array([[_xp, ytop]]))[0][1]; _rt = np.where(rm.any(1))[0].min()
CROWN_DZ = float(np.clip((_rt - _vt) * mmpp, -0.004, 0.004)) if _os.environ.get('SCP_CROWN_DZ', '1') != '0' and '--novertical' not in sys.argv else 0.0
print('crown height: render top row %d, photo top lands on row %.1f -> dz %.2f mm' % (_rt, _vt, CROWN_DZ * 1e3))
def apply(a, P):
    Q = P.copy(); p = prof(P[:, 2]); Q[:, 0] *= 1 + a[0] * p + a[1] * p ** 3; Q[:, 2] += CROWN_DZ * p; return Q
def res(a): return np.array([(width_of(apply(a, P0), z) - w0) for z, w0 in zip(Z[ok], W0[ok])]) - DEF[ok]
a = np.zeros(2)
if ok.sum() >= 4:
    sol = least_squares(lambda a: np.r_[np.nan_to_num(res(a)), 0.0005 * a], np.zeros(2), bounds=([-0.3, -0.3], [0.1, 0.1]), diff_step=0.02); a = sol.x

def crown(P):
    """returns (newP, info) or (P, None). Widths in metres."""
    ok_ = prof(Z) > 0.02
    if ok_.sum() < 6: return P, None
    zr = Z[ok_]; o = np.argsort(zr); zr = zr[o]; hpr = HPW[ok_][o]
    sig = 2
    hps = ndi.gaussian_filter1d(hpr, sig, mode='nearest')
    zt = P[:, 2].max(); i0 = int(np.argmax(hps)); zm = zr[i0]; Wm = hps[i0]
    if zt - zm < 0.01: return P, None
    zr_top = zr.max()
    # photo half-width profile (smoothed); above the highest photo row a sqrt taper to the render's top. The earlier superellipse cap (n=2.4) worsened the silhouette against a correct mask (char2 skull IoU .976 -> .935): the
    # round crown comes from a correct mask (prep dome fit), not from a prior here.
    HT = lambda z: np.where(z <= zr_top, np.interp(z, zr, hps), hps[-1] * np.sqrt(np.clip(1 - (z - zr_top) / max(zt - zr_top, 1e-4), 0, 1)))
    zg_ = np.linspace(zb, zt, 40)[1:-1]; bw = (zt - zb) / 39
    HRg, XCg = [], []
    for zc_ in zg_:
        m_ = np.abs(P[:, 2] - zc_) < max(bw * 0.75, 0.0025)
        if m_.sum() < 6: HRg.append(np.nan); XCg.append(np.nan); continue
        x_ = P[m_, 0]; HRg.append((x_.max() - x_.min()) / 2); XCg.append((x_.max() + x_.min()) / 2)
    HRg = np.array(HRg); XCg = np.array(XCg); g = np.isfinite(HRg)
    if g.sum() < 8: return P, None
    HRg = np.interp(zg_, zg_[g], HRg[g]); XCg = np.interp(zg_, zg_[g], XCg[g])
    HRg = ndi.gaussian_filter1d(HRg, 1.5, mode='nearest'); XCg = ndi.gaussian_filter1d(XCg, 1.5, mode='nearest')
    sg = np.clip(HT(zg_) / np.maximum(HRg, 1e-4), 0.6, 2.5); sg = ndi.gaussian_filter1d(sg, 1.5, mode='nearest')
    def move(Pi):
        Q = Pi.copy(); z = Pi[:, 2]; pp = prof(z); sx = np.interp(z, zg_, sg); xc = np.interp(z, zg_, XCg)
        Q[:, 0] = Pi[:, 0] + pp * ((Pi[:, 0] - xc) * sx - Pi[:, 0]); return Q
    Q = move(P)
    def err(Pi):
        e_ = [(width_of(Pi, z) - HT(z)) for z in zr]; e_ = np.array(e_); return float(np.sqrt(np.nanmean(e_ ** 2)))
    return Q, dict(before=err(P), after=err(Q), zm=float(zm), Wm=float(Wm))
newP = apply(a, P0); fl = int(count_flips(t, newP).sum())
print('skull width: rows %d, deficit mean %.2f mm (photo - render half width), a=%s, residual rms %.2f -> %.2f mm, flips %d' % (ok.sum(), DEF[ok].mean() * 1e3 if ok.any() else 0, np.round(a, 3), np.sqrt(np.mean(DEF[ok] ** 2)) * 1e3 if ok.any() else 0,
      np.sqrt(np.mean(res(a) ** 2)) * 1e3 if ok.any() else 0, fl))
if fl: newP, a = P0, np.zeros(2); print('flips -> step skipped')
crown_info = None
if '--nocrown' not in sys.argv:
    Qc, ci = crown(newP)
    if ci is not None:
        flc = int(count_flips(t, Qc).sum()); good = flc == 0 and ci['after'] < 0.85 * ci['before']
        print('crown: widest row z %.1f mm half-width %.1f mm; dome width rms %.2f -> %.2f mm, flips %d -> %s' % (ci['zm'] * 1e3, ci['Wm'] * 1e3, ci['before'] * 1e3, ci['after'] * 1e3, flc, 'APPLIED' if good else 'skipped'))
        if good: newP = Qc; crown_info = ci
    else: print('crown: not enough rows, skipped')
dB = newP - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = newP
np.save(FIT + f'wrap_r{tag}_basis.npy', newP.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=newP, f=t.faces)
json.dump(dict(a=a.tolist(), crown=crown_info, crown_dz_mm=CROWN_DZ * 1e3), open(FIT + 'skullw.json', 'w'))
try:     # keep the report's 3D skin statistics in step with the reshaped dome (the scorecard reads them)
    import r6_metrics as MT
    rp_ = json.load(open(FIT + f'wrap_r{tag}_report.json')); rp_['target'] = MT.tgt_stats(newP); json.dump(rp_, open(FIT + f'wrap_r{tag}_report.json', 'w'), indent=1, default=float); print('3D target after skull step', rp_['target'])
except Exception as ex_: print('report 3D stats not updated:', ex_)
print('SKULLW DONE')
