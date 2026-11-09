"""Round 6 closed-loop fit: reusable SLIDERS (template shape keys, 83 originals + 34 SC_) + the 16-parameter r5 global warp, detector in the loop.
theta = [16 warp params | slider coefficients]. A slider column is a signed coefficient a: a>0 applies the '+' key delta, a<0 the '-' key delta (exact negative twins for SC_, near twins for the originals);
one-sided originals have [0,1]. L/R-asymmetric originals, gaze/blink/wink/squint expressions, General/Neck/Shoulders/Cartoonish and SC_Nose_Length/Ear_* are not fitted.
Jacobian of the 13 photo ratios is measured by rendering with each parameter perturbed (first iteration), then Broyden-updated from the single r0 measurement of every later iteration.
usage: r6_06_sliderloop.py <in_basis.npy> <render_lm.json> <cam.json> <render.png> <tag> [--iters 5] [--tr 0.35] [--dscale 0.5] [--sdelta 0.5] [--cols sliders|warp|all] [--warpreg 1] [--sreg 0.12] + r6_02_warp options"""
import sys, subprocess, time, json, os
import numpy as np
import argparse
pre = argparse.ArgumentParser(); pre.add_argument('--iters', type=int, default=5); pre.add_argument('--tr', type=float, default=0.35); pre.add_argument('--dscale', type=float, default=0.5)
pre.add_argument('--sdelta', type=float, default=0.5); pre.add_argument('--cols', default='all'); pre.add_argument('--warpreg', type=float, default=1.0); pre.add_argument('--sreg', type=float, default=0.12)
pre.add_argument('--maxnfev', type=int, default=40); pre.add_argument('--remeasure', type=int, default=99)
pa, rest = pre.parse_known_args(); sys.argv = [sys.argv[0]] + rest
import r6_02_warp as W
from r6_common import *
import fit_checks6 as FC
from scipy.optimize import least_squares
tag = W.A.tag; SC = REPO + 'Art/Blender/tools/experimental/face_template/'; t = W.t
# ---------------- slider columns
PAIRS = [('Head_Width_Up', 'Head_Width_Dn'), ('Head_Cheek_Out', 'Head_Cheek_In'), ('Head_Height_Up', 'Head_Height_Dn'), ('Head_Thick', 'Head_Thin'), ('Head_Vol_Up', 'Head_Vol_Dn'),
         ('JawLine_Widen', 'JawLine_Narrow'), ('Jaw_Widen', 'Jaw_Narrow'), ('Jaw_Sides_Up', 'Jaw_Sides_Dn'), ('Face_Lower_Out', 'Face_Lower_In'), ('Nose_Wings_Out', 'Nose_Wings_In'),
         ('Nose_Wings_Up', 'Nose_Wings_Dn'), ('Nose_Up', 'Nose_Dn'), ('Nose_Long', 'Nose_Short'), ('Nose_Open', 'Nose_Close'), ('Nose_Tip_Up', 'Nose_Tip_Down'),
         ('Jaw_Chin_Long', 'Jaw_Chin_Short'), ('Jaw_Chin_Sharp', 'Jaw_Chin_Smooth'), ('Jaw_Sharp', 'Jaw_Smooth')]
SINGLES = ['Nose_Large', 'Nose_Sharp', 'Nose_Bulge', 'Lips_Large', 'Lips_Narrow', 'Lips_Tighten']
SCN = ['Midface_Height', 'LowerFace_Height', 'Forehead_Height', 'Cheekbone_Width', 'Eye_Size', 'Eye_Spacing', 'Lip_Fullness', 'Lip_Width', 'Brow_Lift', 'Nose_Width', 'Skull_Depth', 'Eye_Almond', 'Eye_Width', 'UpperLid_Heavy']
COLS = []   # (name, +key, -key|None, lo, hi)
for a, b in PAIRS: COLS.append((a + '~' + b, a, b, -1.0, 1.0))
for a in SINGLES: COLS.append((a, a, None, 0.0, 1.0))
for n in SCN: COLS.append(('SC_' + n, 'SC_' + n + '+', 'SC_' + n + '-', -1.0, 1.0))
Dp = np.stack([t.keys[t.kidx(c[1])] - t.basis for c in COLS]); Dm = np.stack([(t.keys[t.kidx(c[2])] - t.basis) if c[2] else np.zeros_like(t.basis) for c in COLS])
SNAMES = [c[0] for c in COLS]; nw = len(W.NAMES); WNAMES = list(W.NAMES); NAMES = WNAMES + SNAMES; n = len(NAMES)
S_LO = np.array([c[3] for c in COLS]); S_HI = np.array([c[4] for c in COLS])
def slider_disp(a):
    return np.tensordot(np.maximum(a, 0), Dp, 1) + np.tensordot(np.maximum(-a, 0), Dm, 1)
W_SIG = W.SIG.copy() / pa.warpreg; W_LO = W.LO.copy(); W_HI = W.HI.copy(); warp0 = W.warp
W.NAMES = NAMES; W.LO = np.r_[W_LO, S_LO]; W.HI = np.r_[W_HI, S_HI]; W.SIG = np.r_[W_SIG, np.full(len(COLS), W.A.reg / pa.sreg)]   # regulariser term = A.reg*th/SIG -> weight per unit slider coefficient = sreg
def warp6(th):
    th = np.asarray(th, float); Pw = warp0(th[:nw]); a = th[nw:]
    if not np.any(a): return Pw
    return deflip(t, Pw, Pw + slider_disp(a))      # sliders alone never flip a face: the slider displacement is damped locally where it would
W.warp = warp6
EV0 = np.concatenate([t.eyeL, t.eyeR]).astype(float); EF = np.concatenate([t.eyeL_f, t.eyeR_f + 512])
ANC = {s: FC.eye_anchor(t, s) for s in ('L', 'R')}
def eyes_for(P):   # eyeballs follow the lower lid ring + corners (translation + uniform scale about the eyeball centre), as c6_00_reseat
    ev = EV0.copy()
    for k, s in enumerate(('L', 'R')):
        ring = ANC[s]; a, b = t.basis[ring], P[ring]; sl = slice(512 * k, 512 * (k + 1)); c0 = ev[sl].mean(0)
        sc = np.linalg.norm(b - b.mean(0), axis=1).mean() / np.linalg.norm(a - a.mean(0), axis=1).mean()
        ev[sl] = c0 + sc * (ev[sl] - c0) + (b.mean(0) - a.mean(0))
    return ev
def measure(ths, work):
    fs = []
    for i, th in enumerate(ths):
        P = W.ysnap(W.warp(th)); f = work + f'_m{i}.npz'; np.savez(f, v=P, f=t.faces, ev=eyes_for(P), ef=EF); fs.append(f)
    r = subprocess.run([BLENDER, '-b', '-P', SC + 'r6_01b_batch_blender.py', '--', '1.46', '1.92', work + '_i'] + fs, capture_output=True, text=True)
    assert 'OK' in r.stdout, r.stdout[-500:] + r.stderr[-500:]
    pngs = [work + f'_i{i}.png' for i in range(len(ths))]
    subprocess.run([MP, SC + 'r6_01c_batch_lm.py', work + '_lm.json'] + pngs, check=True, capture_output=True)
    lm = json.load(open(work + '_lm.json')); return [W.ratios(np.array(lm[p])) for p in pngs]
keys = list(W.photo_r)
PENTOL = float(os.environ.get('PENTOL', '0.45'))   # round 6: lid-penetration gate template+0.45 mm (r5: +0.25; the base already sits at +0.22, any eye slider needs room)
DEPTH_ONLY = {'snl', 'sly'}
_P0s = W.ysnap(W.warp(np.zeros(n))); _G0 = W.gates(_P0s); _F0 = W.flips_of(_P0s)
def feasible(th):
    P = W.ysnap(W.warp(th)); G = W.gates(P)
    for k, v in G.items():
        if k.startswith('blink_'):
            b0 = _G0[k]
            if v['pen_moved_mm'] > max(v['pen_template_mm'] + PENTOL, b0['pen_moved_mm']) + 0.03: return False
            if v['gap_max_mm'] > max(v['tpl_gap_max_mm'] + 0.5, b0['gap_max_mm']) + 0.03: return False
            if v['gap_mean_mm'] > max(v['tpl_gap_mean_mm'] + 0.5, b0['gap_mean_mm']) + 0.03: return False
        if k.startswith('lip_') and v['min_gap_mm'] < -0.3: return False
    if any(v['min_ratio'] < 0.7 or v['max_ratio'] > 1.4 or v['flips'] > 0 for v in G['rings'].values()): return False
    return W.flips_of(P) <= _F0
def make_feasible(th_ok, th_new):
    if feasible(th_new): return th_new
    lo_, hi_ = 0.0, 1.0
    for _ in range(9):
        m = (lo_ + hi_) / 2
        if feasible(th_ok + m * (th_new - th_ok)): lo_ = m
        else: hi_ = m
    print('  gates: step shortened to %.2f of the solve' % lo_); return th_ok + lo_ * (th_new - th_ok)
def cost_from(r0v, th, J, th0):
    r0 = r0v; W.RAT = lambda P, t_: dict(zip(keys, r0 + J @ (np.asarray(t_) - th0)))
    return W.residual(th)
th = np.zeros(n)
if W.A.seed: sd = json.load(open(W.A.seed)); sd = sd.get('theta', sd); th = np.array([sd.get(k, 0.0) for k in NAMES])
fixed = {kv.split('=')[0]: float(kv.split('=')[1]) for kv in W.A.fix.split(',') if kv}
for k, v in fixed.items(): th[NAMES.index(k)] = v
def is_free(i):
    k = NAMES[i]
    if k in fixed: return False
    if pa.cols == 'sliders' and i < nw: return False
    if pa.cols == 'warp' and i >= nw: return False
    return not W.A.free or k in W.A.free.split(',')
free = [i for i in range(n) if is_free(i)]
print('n params', n, 'free', len(free), 'base feasible:', feasible(np.zeros(n)), 'seed feasible:', feasible(th))
if not feasible(th): th = make_feasible(np.zeros(n), th)
work = FIT + f'_cl_{tag}'; best = (1e18, th.copy(), None); tr = pa.tr; hist = []; J = None; th_prev = None; r_prev = None
for it in range(pa.iters):
    t0 = time.time(); fm = [i for i in free if NAMES[i] not in DEPTH_ONLY]
    dl = {i: (pa.dscale * W.SIG[i] if i < nw else pa.sdelta) for i in fm}
    if J is None or it % pa.remeasure == 0:
        ths = [th.copy()] + [th + np.eye(n)[i] * dl[i] for i in fm]; rs = measure(ths, work); r0 = rs[0]; r0v = np.array([r0[k] for k in keys])
        J = np.zeros((len(keys), n))
        for j, i in enumerate(fm): J[:, i] = (np.array([rs[j + 1][k] for k in keys]) - r0v) / dl[i]
    else:
        r0 = measure([th], work)[0]; r0v = np.array([r0[k] for k in keys]); d = th - th_prev
        if d @ d > 1e-12: J += np.outer(r0v - r_prev - J @ d, d) / (d @ d)
    th_prev = th.copy(); r_prev = r0v.copy()
    c0 = cost_from(r0v, th, J, th); cost = float((c0 ** 2).sum()); hist.append(cost)
    err = {k: 100 * (r0[k] / W.photo_r[k] - 1) for k in keys}
    print('iter %d true cost %.1f ratio MAE(11) %.2f%%  (%.0fs)' % (it, cost, np.mean([abs(err[k]) for k in keys[:11]]), time.time() - t0), {k: round(v, 1) for k, v in err.items() if abs(v) > 3}, flush=True)
    if cost < best[0] and feasible(th): best = (cost, th.copy(), err)
    else:
        tr *= 0.5; th = best[1].copy(); print('  no gain, shrink trust region ->', tr)
        if tr < 0.03: break
    lo = np.maximum(W.LO, th - tr * (W.HI - W.LO)); hi = np.minimum(W.HI, th + tr * (W.HI - W.LO))
    for i in range(n):
        if i not in free: lo[i] = hi[i] = th[i]; hi[i] += 1e-9
    th0 = th.copy(); r_anchor = r_prev     # linear model anchored at the measured point th_prev
    sol = least_squares(lambda v: cost_from(r_anchor, np.where(np.isin(np.arange(n), free), v, th0), J, th_prev), th0, bounds=(lo, hi), x_scale=W.SIG, diff_step=1e-3, max_nfev=pa.maxnfev)
    th = make_feasible(th0, sol.x)
    print('  theta warp', {k: round(float(v), 3) for k, v in zip(WNAMES, th[:nw])}); print('  sliders', {k: round(float(v), 2) for k, v in zip(SNAMES, th[nw:]) if abs(v) > 0.02}, flush=True)
rs = measure([th], work); c = cost_from(np.array([rs[0][k] for k in keys]), th, J, th); cost = float((c ** 2).sum()); err = {k: 100 * (rs[0][k] / W.photo_r[k] - 1) for k in keys}
print('final true cost %.1f' % cost)
if cost < best[0] and feasible(th): best = (cost, th.copy(), err)
cost, th, err = best; W.RAT = lambda P, t_: W.ratios(W.lm_model(P))
P = W.ysnap(W.warp(th)); print('BEST cost %.1f' % cost); print('ratio err %:', {k: round(float(v), 1) for k, v in err.items()}); print('MAE(11) %.2f' % np.mean([abs(err[k]) for k in keys[:11]]))
print('theta', {k: round(float(v), 3) for k, v in zip(NAMES, th) if abs(v) > 1e-3})
st = W.MT.tgt_stats(P); print('3D (saved, y-snapped mesh)', st); chk = W.gates(P); print('GATES PASS=%s flips %d' % (chk['PASS'], W.flips_of(P)))
dB = P - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = P
np.save(FIT + f'wrap_r{tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=P, f=t.faces)
json.dump(dict(theta=dict(zip(NAMES, map(float, th))), ratio_err_pct=err, history=hist, checks=chk, target=st, args=vars(W.A)), open(FIT + f'warp_{tag}.json', 'w'), indent=1, default=float)
json.dump(dict(theta=dict(zip(NAMES, map(float, th)))), open(FIT + f'wrap_r{tag}_report.json', 'w'))
