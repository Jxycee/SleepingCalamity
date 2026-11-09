"""Round 5 LOW-ORDER GLOBAL WARP (replaces more local TPS). A smooth ~12-parameter warp of the head, fitted automatically (bounded least squares) to the photo.
All windows/anchors come from the template's own landmarks (no per-face numbers); the parameters are the only per-character output.
Parameters (theta):  skull_dz (mm, skull above brow), s_en / s_nm / s_mc (vertical scale of eye->nose base / nose base->mouth / mouth->chin zones),
  sw (face width at cheek level), sj (jaw width), snw (nose/alar width), snl (nose protrusion about the cheek plane), slz (lip vermilion vertical scale),
  sly (mm, lip outward push), brow (mm, brow lift), se (uniform eye-zone scale about each eye ring centroid; eyeballs follow in c6_00_reseat).
Fit residuals: the photo's 11 front ratios (+ eye width/height) from the detector's landmarks on the base render + geometric delta of the warp (closed loop: re-render and
re-run to recalibrate), photo top of head, 3D-target point-to-surface RMS (skin/nose/lips/brow), ring-edge gates, ridge regulariser.
usage: r6_02_warp.py <in_basis.npy> <render_lm.json> <cam.json> <render.png> <tag> [--w3 1] [--skincap 0.62] [--reg 0.15] [--fix name=val,...] [--free list]
writes fit6/wrap_r<tag>_{basis,keys,mesh}, warp_<tag>.json"""
import json, argparse
from PIL import Image
from scipy import ndimage as ndi
from scipy.optimize import least_squares
from r6_common import *
from fit_checks6 import run_checks
import r6_metrics as MT
ap = argparse.ArgumentParser(); [ap.add_argument(a) for a in ('inb', 'lmj', 'camj', 'png', 'tag')]
ap.add_argument('--w3', type=float, default=1.0); ap.add_argument('--skincap', type=float, default=0.62); ap.add_argument('--reg', type=float, default=0.15)
ap.add_argument('--fix', default=''); ap.add_argument('--free', default=''); ap.add_argument('--wnose', type=float, default=1.0); ap.add_argument('--wlips', type=float, default=1.0)
ap.add_argument('--wskin', type=float, default=1.0); ap.add_argument('--snapw', type=float, default=0.7); ap.add_argument('--seed', default='', help='initial theta json'); ap.add_argument('--scale', type=float, default=1.0, help='bound scale')
A = ap.parse_args()
t = Tpl(); P0 = np.load(A.inb).astype(np.float64); cam = json.load(open(A.camj)); R_, S_, cx, cz = cam['R'], cam['S'], cam['cx'], cam['cz']
mm = lambda x: x * 1e-3
# ---------------- photo targets
ph = np.array(json.load(open(TGT + 'landmarks.json'))['landmarks_px_xyz'])[:468, :2]
rl = json.load(open(A.lmj)); rp = np.array([rl[str(i)] for i in range(468)])
px2mm = lambda p: np.stack([cx + (p[:, 0] / R_ - 0.5) * S_, cz + (0.5 - p[:, 1] / R_) * S_], 1) * 1e3     # (x mm, z mm) of render px
base_xy = px2mm(rp)                                                                                     # detector landmarks on the base render, mm
def ratios(L):   # L: (468,2) with y = -z (px convention: down is +)
    d = lambda a, b: np.linalg.norm(L[a] - L[b]); io = d(33, 263); eyec = (L[33] + L[263]) / 2
    return {'nose_width': d(129, 358) / io, 'mouth_width': d(61, 291) / io, 'face_width': d(234, 454) / io, 'jaw_width': d(172, 397) / io,
            'brow_eye_gap': ((abs(L[105][1] - L[159][1]) + abs(L[334][1] - L[386][1])) / 2) / io, 'eye_to_nose_base': (L[2][1] - eyec[1]) / io,
            'nose_to_mouth': (L[13][1] - L[2][1]) / io, 'mouth_to_chin': (L[152][1] - L[13][1]) / io, 'eye_to_chin': (L[152][1] - eyec[1]) / io,
            'forehead_eye': (eyec[1] - L[10][1]) / io, 'lip_thickness': d(0, 17) / io,
            'eye_w': (d(33, 133) + d(263, 362)) / 2 / io, 'eye_h': (d(159, 145) + d(386, 374)) / 2 / io}
photo_r = ratios(ph)
hits = json.load(open(FIT + 'tpl_hits.json')); lid = np.array([i for i in range(468) if str(i) in hits and hits[str(i)] is not None and np.asarray(hits[str(i)]).shape == (3,)])
need = [33, 263, 133, 362, 129, 358, 61, 291, 234, 454, 172, 397, 105, 159, 334, 386, 2, 13, 152, 10, 0, 17, 145, 374]
kd = cKDTree(t.basis); H = np.array([hits[str(i)] for i in lid]); dd_, ii_ = kd.query(H, k=6); ww_ = 1 / (dd_ + 1e-3) ** 2; ww_ /= ww_.sum(1, keepdims=True)
# lid landmarks have no template hit (ray passes through the eye hole): attach them to the eyelid-ring vertex nearest in (x, z) to the base-render landmark
miss = [i for i in need if i not in set(lid.tolist())]; ring_all = np.concatenate([t.S('eyelid_ring_L'), t.S('eyelid_ring_R')]).astype(int)
for i in miss:
    q = base_xy[i] * 1e-3; j = ring_all[np.argmin(np.linalg.norm(P0[ring_all][:, [0, 2]] - q, axis=1))]
    lid = np.append(lid, i); ii_ = np.vstack([ii_, np.full((1, 6), j)]); ww_ = np.vstack([ww_, np.eye(6)[0:1] * 0 + 1 / 6])
lpos = {int(i): k for k, i in enumerate(lid)}
def geo(P):   # landmark positions (L,3) m of the mesh P (IDW of the vertices around each template hit)
    return (P[ii_] * ww_[:, :, None]).sum(1)
G0 = geo(P0)
def lm_model(P):   # detector landmarks on the base render + geometric displacement of the warp (x, -z in mm)
    L = base_xy.copy(); dG = (geo(P) - G0) * 1e3
    for i, k in lpos.items(): L[i] = base_xy[i] + np.array([dG[k, 0], dG[k, 2]])
    return np.c_[L[:, 0], -L[:, 1]]
# photo top of head (as r4_02): photo-render offset of the head top in mm
EC = [33, 263, 133, 362]; s2, Rr, tt = umeyama(np.c_[rp[EC], np.zeros(4)], np.c_[ph[EC], np.zeros(4)], True); Rr2 = Rr[:2, :2]; inv = lambda q: ((q - tt[:2]) @ Rr2) / s2
mv = np.array(Image.open(TGT + 'mask_vis.png').convert('RGB')).astype(int); head = (mv[..., 0] - mv[..., 1]) <= 50
head = ndi.binary_opening(head, iterations=2); lab, n = ndi.label(head); head = ndi.binary_fill_holes(lab == lab[int(ph[168, 1]), int(ph[168, 0])])
ytop = np.where(head.any(1))[0].min(); xm = np.where(head[ytop + 3])[0].mean(); u = inv(np.array([[xm, ytop]]))[0]; z_ph_top = (cz + (0.5 - u[1] / R_) * S_) * 1e3
# ---------------- anchors from the template's own landmarks (positions on the base mesh P0)
LN = lambda n: P0[t.lm_idx[t.lm_names.index(n)]]
ze = float(np.mean([LN(n)[2] for n in ('eye_inner_L', 'eye_outer_L', 'eye_inner_R', 'eye_outer_R')])); zn = LN('subnasale')[2]; zm = LN('lip_seam_center')[2]; zc = LN('chin_tip')[2]
zb = t.basis[t.lm_idx[t.lm_names.index('brow_center')], 2]; ztop = P0[:, 2].max()
y_temple = np.mean([LN('temple_L')[1], LN('temple_R')[1]]); y_lobe = np.mean([LN('ear_lobe_L')[1], LN('ear_lobe_R')[1]]); y_cheek = np.mean([LN('cheekbone_L')[1], LN('cheekbone_R')[1]])
ss = lambda a, b, x: (lambda q: q * q * (3 - 2 * q))(np.clip((x - a) / (b - a), 0, 1))
X, Y, Z = P0[:, 0], P0[:, 1], P0[:, 2]
# zone vertical-scale profile (integrated displacement of a piecewise scale, smooth boundaries 3 mm, faded to 0 at the neck pins as in r4_03)
zg = np.linspace(zc - 0.12, ze + 0.06, 2400); sm_ = 0.003
box = lambda hi, lo: ss(lo - sm_, lo + sm_, zg) * (1 - ss(hi - sm_, hi + sm_, zg))
W_en, W_nm, W_mc = box(ze, zn), box(zn, zm), box(zm, zc)
def cum_from_top(W):   # integral of W from z up to the eye line (>= 0), per grid point
    dz = np.diff(zg); c = np.concatenate([[0], np.cumsum((W[1:] + W[:-1]) / 2 * dz)]); return c[-1] - c
CUM = {'en': cum_from_top(W_en), 'nm': cum_from_top(W_nm), 'mc': cum_from_top(W_mc)}
ft = ss(zc - 0.045, zc + 0.005, Z)
ZONE = {k: -np.interp(Z, zg, v) * ft for k, v in CUM.items()}     # dz per unit (s-1): z' = z - (s-1) * integral  (lower parts move down when the zone grows)
skull_prof = ss(zb, ztop, Z)
# windows
G3 = lambda ids: G0[[lpos[i] for i in ids]]
BROW = [70, 63, 105, 66, 107, 55, 65, 52, 53, 46, 300, 293, 334, 296, 336, 285, 295, 282, 283, 276]
w_brow = 1 - ss(0.008, 0.020, cKDTree(G3(BROW)).query(P0)[0]); w_brow *= (Y < y_temple)
axis = G3([168, 6, 197, 195, 5, 4, 1, 2]); aw = np.array([1, 1, 2.0]); kax = cKDTree(np.concatenate([np.linspace(axis[i], axis[i + 1], 12) for i in range(len(axis) - 1)]) * aw)
dax = kax.query(P0 * aw)[0]; w_nl = 1 - ss(0.012, 0.022, dax); w_nw = 1 - ss(0.030, 0.042, dax)
dl = cKDTree(P0[t.S('lip_line')]).query(P0)[0]; w_lip = 1 - ss(0.006, 0.013, dl); zseam = P0[t.S('lip_line')][:, 2].mean()
w_mw = 1 - ss(0.014, 0.026, dl)
YF = 1 - ss(y_temple - 0.010, y_lobe, Y)
w_sw = ss(zm - 0.015, zn, Z) * (1 - ss(zb, zb + 0.030, Z)) * YF       # cheek level
w_sj = ss(zc - 0.035, zc + 0.006, Z) * (1 - ss(zn, zn + 0.010, Z)) * YF  # jaw level (mouth to chin)
xeo = np.mean([abs(LN('eye_outer_L')[0]), abs(LN('eye_outer_R')[0])]); xmc = np.mean([abs(LN('mouth_corner_L')[0]), abs(LN('mouth_corner_R')[0])])
XP_W = np.sign(X) * ss(xeo, xeo + 0.025, np.abs(X)); XP_J = np.sign(X) * ss(xmc - 0.005, xmc + 0.030, np.abs(X))
RINGS = {s: t.S('eyelid_ring_' + s).astype(int) for s in ('L', 'R')}
EYEW = {}
for s in ('L', 'R'):
    dd = cKDTree(P0[RINGS[s]]).query(P0)[0]; EYEW[s] = (dd, ((X > 0) == (s == 'L')))
NAMES = ['skull_dz', 's_en', 's_nm', 's_mc', 'sw', 'sj', 'snw', 'snl', 'slz', 'sly', 'brow', 'se', 'sez', 'eye_dz', 'nose_dz', 'smw']
LO = np.array([-12, -.30, -.30, -.25, -4, -5, -.15, -.20, -.30, -1.5, -8, -.10, -.10, -6, -6, -.15]); HI = np.array([8, .30, .30, .25, 10, 8, .20, .25, .60, 4.0, 5, .25, .30, 6, 6, .10])
SIG = np.array([4, .1, .1, .1, 3, 3, .07, .1, .2, 1.5, 2, .08, .1, 2.5, 2.5, .05])
def warp(th):
    th = dict(zip(NAMES, th)); d = np.zeros_like(P0)
    d[:, 2] += mm(th['skull_dz']) * skull_prof
    d[:, 2] += th['s_en'] * ZONE['en'] + th['s_nm'] * ZONE['nm'] + th['s_mc'] * ZONE['mc']
    d[:, 0] += mm(th['sw']) * w_sw * XP_W + mm(th['sj']) * w_sj * XP_J      # lateral push (mm) outside the eye / mouth-corner x: IOD and mouth width stay put
    d[:, 0] += th['snw'] * w_nw * (X - LN('nose_tip')[0]); d[:, 1] += th['snl'] * w_nl * (Y - y_cheek)
    d[:, 2] += th['slz'] * w_lip * (Z - zseam); d[:, 1] += -mm(th['sly']) * w_lip
    d[:, 0] += th['smw'] * w_mw * X                                    # mouth width (about the midline, window = lips + corners)
    d[:, 2] += mm(th['brow']) * w_brow
    for s_ in ('L', 'R'): d[:, 2] += mm(th['eye_dz']) * (1 - ss(0.008, 0.030, EYEW[s_][0])) * EYEW[s_][1]    # eye zone vertical shift (smooth window); brows/cheeks follow partly
    d[:, 2] += mm(th['nose_dz']) * w_nw                                   # nose / alar zone vertical shift (3D target decides)
    # eyes: the whole eye zone moves rigidly (mean displacement of the ring) and is then scaled uniformly about the ring centroid
    for s in ('L', 'R'):
        dd, side = EYEW[s]; w = (1 - ss(0.006, 0.016, dd)) * side; m = d[RINGS[s]].mean(0); d = w[:, None] * m + (1 - w[:, None]) * d
        c = P0[RINGS[s]].mean(0) + m; w2 = (1 - ss(0.006, 0.020, dd)) * side; d += w2[:, None] * th['se'] * (P0 + m - c); d[:, 2] += w2 * th['sez'] * (P0 + m - c)[:, 2]
    return P0 + d
_sk = np.where(MT.M['skin'] & ~(MT.M['nose'] | MT.M['lips'] | MT.M['brow']))[0]
_reps = np.unique(t.rep); _nid = -np.ones(len(t.rep), int); _nid[_reps] = np.arange(len(_reps)); _wof = _nid[t.rep]
_wf = _wof[t.faces]; _wf = _wf[(_wf[:, 0] != _wf[:, 1]) & (_wf[:, 1] != _wf[:, 2]) & (_wf[:, 0] != _wf[:, 2])]
_E = edges_of(_wf); _nW = len(_reps); _adj = sp.coo_matrix((np.ones(len(_E)), (_E[:, 0], _E[:, 1])), shape=(_nW, _nW)); _adj = (_adj + _adj.T).tocsr(); _deg = np.maximum(np.asarray(_adj.sum(1)).ravel(), 1)
def ysnap(P):   # model of the correction stage's snap: skin (not nose/lips/brow) is pulled back onto the 3D target along y (limited, as r6_03_corr snapmode y)
    Q = P.copy(); dq, q, nq = MT._surf.query(P[_sk]); sd = ((P[_sk] - q) * nq).sum(1); ny = nq[:, 1]
    dy = np.where((np.abs(ny) > 0.45) & (np.abs(sd) < 0.008), -sd / np.where(np.abs(ny) > 0.45, ny, 1.0), 0.0); dyv = np.zeros(len(P)); dyv[_sk] = np.clip(dy, -0.004, 0.004) * A.snapw
    wd = np.zeros(_nW); wd[_wof] = dyv       # smooth the snap field on the welded graph (the target's fine bumps must not become skin bumps)
    for _ in range(25): wd = 0.4 * wd + 0.6 * (_adj @ wd) / _deg
    Q[:, 1] += wd[_wof] * (np.abs(wd[_wof]) > 0) * 1.0; Q[~np.isin(np.arange(len(P)), _sk) & (np.abs(wd[_wof]) < 1e-9), 1] += 0
    return deflip(t, P, Q)
def flips_of(P):
    f = lambda Q: (lambda tri: (lambda n: n / (np.linalg.norm(n, axis=1)[:, None] + 1e-30))(np.cross(tri[:, 1] - tri[:, 0], tri[:, 2] - tri[:, 0])))(Q[t.faces])
    return int(((f(P) * f(t.basis)).sum(1) < 0).sum())
def gates(P):
    nk = t.keys + (P - t.basis)[None]; nk[0] = P; return run_checks(t, nk, 'w', verbose=False)
RAT = lambda P, th: ratios(lm_model(P))     # ratio predictor (closed loop replaces it by the detector-measured linearisation)
EXTRA_W = {'eye_w': 0.5, 'eye_h': 0.5}
def residual(u, detail=False):
    th = u
    P = warp(th); r = RAT(P, th); res = []
    P = ysnap(P)
    for k in photo_r: res.append(EXTRA_W.get(k, 1.0) * 100 * (r[k] / photo_r[k] - 1) / 1.0)
    res.append((P[:, 2].max() * 1e3 - z_ph_top) / 2.0 * 2)
    st = MT.tgt_stats(P)
    res += [A.wskin * max(0, st['skin_rms'] - A.skincap) / 0.08, A.wnose * A.w3 * st['nose_rms'] / 1.0, A.wlips * A.w3 * st['lips_rms'] / 0.5, A.w3 * st['brow_rms'] / 0.5]
    G = gates(P); c = G['rings']; res += [max(0, v['pen_moved_mm'] - v['pen_template_mm'] - 0.4) / 0.05 + max(0, v['gap_max_mm'] - v['tpl_gap_max_mm'] - 0.45) / 0.1 + max(0, v['gap_mean_mm'] - v['tpl_gap_mean_mm'] - 0.4) / 0.05 for k, v in G.items() if k.startswith('blink_')]; res += [max(0, 0.3 - v['min_gap_mm']) / 0.1 for k, v in G.items() if k.startswith('lip_')]; mn = min(v['min_ratio'] for v in c.values()); mxr = max(v['max_ratio'] for v in c.values())
    res += [max(0, 0.74 - mn) / 0.01, max(0, mxr - 1.30) / 0.03]
    res += list(A.reg * np.asarray(th) / SIG)
    if detail: return P, r, st, c
    return np.array(res)
if __name__ == '__main__':
    u0 = np.zeros(len(NAMES))
    if A.seed: sd = json.load(open(A.seed)); u0 = np.array([sd.get(n, 0.0) for n in NAMES])
    lo, hi = LO * A.scale, HI * A.scale
    fixed = {kv.split('=')[0]: float(kv.split('=')[1]) for kv in A.fix.split(',') if kv}
    free = [n for n in NAMES if (not A.free or n in A.free.split(',')) and n not in fixed]; fi = [NAMES.index(n) for n in free]
    def full(v):
        u = u0.copy()
        for n, x in fixed.items(): u[NAMES.index(n)] = x
        u[fi] = v; return u
    r0 = residual(full(u0[fi])); print('start cost %.1f' % (r0 ** 2).sum())
    res = least_squares(lambda v: residual(full(v)), u0[fi], bounds=(lo[fi], hi[fi]), x_scale=SIG[fi], diff_step=1e-3, max_nfev=60)
    th = full(res.x); _, r, st, c = residual(th, detail=True); P = warp(th); print('end cost %.1f' % (res.fun ** 2).sum())
    lab_ = list(photo_r) + ['top', 'skin', 'nose', 'lips', 'brow'] + ['blink%d' % i for i in range(4)] + ['lip%d' % i for i in range(3)] + ['ringmin', 'ringmax'] + ['reg_' + n for n in NAMES]; print('residual terms >1:', {l: round(float(v), 1) for l, v in zip(lab_, res.fun) if abs(v) > 1.0})
    print('theta', {n: round(float(v), 3) for n, v in zip(NAMES, th)})
    base_r = ratios(lm_model(P0)); print('%-18s %8s %9s %9s' % ('ratio', 'photo', 'base', 'predicted'))
    for k in photo_r: print('%-18s %8.3f %+8.1f%% %+8.1f%%' % (k, photo_r[k], 100 * (base_r[k] / photo_r[k] - 1), 100 * (r[k] / photo_r[k] - 1)))
    print('top z mm: photo %.1f base %.1f new %.1f' % (z_ph_top, P0[:, 2].max() * 1e3, P[:, 2].max() * 1e3))
    print('3D target', st); print('rings', {k: (round(v['min_ratio'], 2), round(v['max_ratio'], 2), v['flips']) for k, v in c.items()}); fl = flips_of(P); print('flips', fl)
    chk = gates(P); print('GATES PASS=%s' % chk['PASS'])
    assert np.abs(P[t.rep] - P).max() < 1e-4, 'weld seams split'
    dB = P - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = P
    np.save(FIT + f'wrap_r{A.tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{A.tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{A.tag}_mesh.npz', v=P, f=t.faces)
    json.dump(dict(theta=dict(zip(NAMES, map(float, th))), args=vars(A), flips=fl, target=st, checks=chk, predicted_ratio_err_pct={k: 100 * (r[k] / photo_r[k] - 1) for k in photo_r}), open(FIT + f'warp_{A.tag}.json', 'w'), indent=1, default=float)
    json.dump(dict(theta=dict(zip(NAMES, map(float, th)))), open(FIT + f'wrap_r{A.tag}_report.json', 'w'), indent=1)
