"""Round 5 closed-loop fit of the global warp: Gauss-Newton with the DETECTOR in the loop. Each iteration renders theta and theta+delta_k (front-ortho Workbench), runs mediapipe,
builds the Jacobian of the 13 photo-ratios w.r.t. the 15 warp parameters, then solves the bounded linearised problem (3D-target / gates / regulariser terms stay exact, see r5_02_warp.residual).
usage: r5_04_closedloop.py <in_basis.npy> <render_lm.json> <cam.json> <render.png> <tag> [--iters 3] [--tr 0.35] + r5_02_warp options"""
import sys, subprocess, time, json, os
import numpy as np
sys.argv = [sys.argv[0]] + sys.argv[1:]
import argparse
pre = argparse.ArgumentParser(); pre.add_argument('--iters', type=int, default=3); pre.add_argument('--tr', type=float, default=0.35); pre.add_argument('--dscale', type=float, default=0.5)
pa, rest = pre.parse_known_args(); sys.argv = [sys.argv[0]] + rest
import r5_02_warp as W
from r5_common import *
from scipy.optimize import least_squares
tag = W.A.tag; names = W.NAMES; n = len(names); SC = REPO + 'Art/Blender/tools/experimental/face_template/'
zt = np.load(D + 'template/tobin_head_template.npz')
EV0 = np.concatenate([zt['eyeL/basis'], zt['eyeR/basis']]).astype(float); EF = np.concatenate([zt['eyeL/faces'], zt['eyeR/faces'] + 512])
def eyes_for(P):   # eyeballs follow the lid rings (translation + uniform scale about the eyeball centre), as c5_00_reseat
    ev = EV0.copy()
    for k, s in enumerate(('L', 'R')):
        ring = W.RINGS[s]; a, b = W.t.basis[ring], P[ring]; sl = slice(512 * k, 512 * (k + 1)); c0 = ev[sl].mean(0)
        sc = np.linalg.norm(b - b.mean(0), axis=1).mean() / np.linalg.norm(a - a.mean(0), axis=1).mean()
        ev[sl] = c0 + sc * (ev[sl] - c0) + (b.mean(0) - a.mean(0))
    return ev
def measure(ths, work):
    """ths: list of theta -> list of detector ratio dicts (rendered after the y-snap model)"""
    fs = []
    for i, th in enumerate(ths):
        P = W.ysnap(W.warp(th)); f = work + f'_m{i}.npz'; np.savez(f, v=P, f=W.t.faces, ev=eyes_for(P), ef=EF); fs.append(f)
    r = subprocess.run([BLENDER, '-b', '-P', SC + 'r5_01b_batch_blender.py', '--', '1.46', '1.92', work + '_i'] + fs, capture_output=True, text=True)
    assert 'OK' in r.stdout, r.stdout[-500:] + r.stderr[-500:]
    pngs = [work + f'_i{i}.png' for i in range(len(ths))]
    subprocess.run([MP, SC + 'r5_01c_batch_lm.py', work + '_lm.json'] + pngs, check=True, capture_output=True)
    lm = json.load(open(work + '_lm.json')); out = []
    for p in pngs:
        L = np.array(lm[p]); out.append(W.ratios(L))
    return out
keys = list(W.photo_r)
DEPTH_ONLY = {'snl', 'sly'}     # invisible to a front view: set by the 3D-target terms only (their detector Jacobian columns are noise)
_P0s = W.ysnap(W.warp(np.zeros(n))); _G0 = W.gates(_P0s); _F0 = W.flips_of(_P0s)
def feasible(th):
    """hard gates: the template-relative gates of fit_checks5 (each criterion may also stay at the level of the base mesh, tag 51, which sits 0.04 mm over the pen gate), ring ratios 0.7-1.4,
    no new flipped faces vs the y-snapped base"""
    P = W.ysnap(W.warp(th)); G = W.gates(P)
    for k, v in G.items():
        if k.startswith('blink_'):
            b0 = _G0[k]
            if v['pen_moved_mm'] > max(v['pen_template_mm'] + 0.25, b0['pen_moved_mm']) + 0.03: return False
            if v['gap_max_mm'] > max(v['tpl_gap_max_mm'] + 0.5, b0['gap_max_mm']) + 0.03: return False
            if v['gap_mean_mm'] > max(v['tpl_gap_mean_mm'] + 0.3, b0['gap_mean_mm']) + 0.03: return False
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
def cost_from(r0_dict, th, J=None, th0=None):
    if J is None: W.RAT = lambda P, t_: r0_dict
    else:
        r0 = np.array([r0_dict[k] for k in keys])
        W.RAT = lambda P, t_: dict(zip(keys, r0 + J @ (np.asarray(t_) - th0)))
    return W.residual(th)
th = np.zeros(n)
if W.A.seed: sd = json.load(open(W.A.seed)); th = np.array([sd.get(k, 0.0) for k in names])
fixed = {kv.split('=')[0]: float(kv.split('=')[1]) for kv in W.A.fix.split(',') if kv}
for k, v in fixed.items(): th[names.index(k)] = v
free = [i for i, k in enumerate(names) if k not in fixed and (not W.A.free or k in W.A.free.split(','))]
print('base feasible:', feasible(np.zeros(n)), 'seed feasible:', feasible(th))
if not feasible(th): th = make_feasible(np.zeros(n), th)
work = FIT + f'_cl_{tag}'; best = (1e18, th.copy(), None); tr = pa.tr; hist = []
for it in range(pa.iters):
    t0 = time.time(); dl = {i: pa.dscale * W.SIG[i] for i in free if names[i] not in DEPTH_ONLY}
    fm = [i for i in free if i in dl]; ths = [th.copy()] + [th + np.eye(n)[i] * dl[i] for i in fm]
    rs = measure(ths, work); r0 = rs[0]
    c0 = cost_from(r0, th); cost = float((c0 ** 2).sum()); hist.append(cost)
    err = {k: 100 * (r0[k] / W.photo_r[k] - 1) for k in keys}
    print('iter %d true cost %.1f ratio MAE(11) %.2f%%  (%.0fs)' % (it, cost, np.mean([abs(err[k]) for k in keys[:11]]), time.time() - t0), {k: round(v, 1) for k, v in err.items() if abs(v) > 3})
    if cost < best[0] and feasible(th): best = (cost, th.copy(), err)
    else: tr *= 0.5; th = best[1].copy(); print('  no gain, shrink trust region ->', tr)
    # Jacobian per unit of parameter (columns for free params)
    J = np.zeros((len(keys), n)); r0v = np.array([r0[k] for k in keys])
    for j, i in enumerate(fm): J[:, i] = (np.array([rs[j + 1][k] for k in keys]) - r0v) / dl[i]
    lo = np.maximum(W.LO, th - tr * (W.HI - W.LO)); hi = np.minimum(W.HI, th + tr * (W.HI - W.LO))
    for i in range(n):
        if i not in free: lo[i] = hi[i] = th[i]; hi[i] += 1e-9
    th0 = th.copy()
    sol = least_squares(lambda v: cost_from(r0, np.where(np.isin(np.arange(n), free), v, th0), J, th0), th0, bounds=(lo, hi), x_scale=W.SIG, diff_step=1e-3, max_nfev=80)
    th = make_feasible(th0, sol.x); print('  theta', {k: round(float(v), 3) for k, v in zip(names, th)})
# final evaluation of the last theta too
rs = measure([th], work); c = cost_from(rs[0], th); cost = float((c ** 2).sum()); err = {k: 100 * (rs[0][k] / W.photo_r[k] - 1) for k in keys}
print('final true cost %.1f' % cost)
if cost < best[0] and feasible(th): best = (cost, th.copy(), err)
cost, th, err = best; W.RAT = lambda P, t_: W.ratios(W.lm_model(P))
P = W.ysnap(W.warp(th)); print('BEST cost %.1f theta' % cost, {k: round(float(v), 3) for k, v in zip(names, th)}); print('ratio err %:', {k: round(v, 1) for k, v in err.items()})
st = W.MT.tgt_stats(P); print('3D (saved, y-snapped mesh)', st); chk = W.gates(P); print('GATES PASS=%s flips %d' % (chk['PASS'], W.flips_of(P)))
dB = P - W.t.basis; nk = (W.t.keys + dB[None]).astype(np.float32); nk[0] = P
np.save(FIT + f'wrap_r{tag}_basis.npy', P.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=P, f=W.t.faces)
json.dump(dict(theta=dict(zip(names, map(float, th))), ratio_err_pct=err, history=hist, checks=chk, args=vars(W.A)), open(FIT + f'warp_{tag}.json', 'w'), indent=1, default=float)
json.dump(dict(theta=dict(zip(names, map(float, th)))), open(FIT + f'wrap_r{tag}_report.json', 'w'))
