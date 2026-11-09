"""Round 12 LOCAL LANDMARK POLISH (after the snap, skull step and slider re-fit). Venv python.
Why: the global warp + sliders fit (r6_06) is a smooth 54-parameter model measured through a noisy detector; for faces far from the template (Cloud) it stalls with every parameter far inside its
bounds (slider coefficients 0.03-0.3 of 1.0, warp parameters at 1-10 % of range) and the ratio errors (eye->nose base -7 %, face width -4.5 %, nose width -4.3 %, mouth->chin +5 %) stay. This stage
closes those residuals directly, per landmark instead of per global parameter:
  1. measure the detector landmarks on the render of the current mesh (same estimator as the scorer: median over 5 re-framings, SCP_LM_JIT=5);
  2. solve for a small (x, z) displacement of the ratio landmarks (nose wings, mouth corners and lip points, cheek / jaw width points, brow, subnasale, chin, forehead; eye landmarks are held fixed, so the
     inter-ocular normaliser and the eye ratios do not move) that brings the 13 photo ratios to the photo values (bounded least squares, ridge on the displacement = smallest change that does the job);
  3. spread the landmark displacements over the mesh with a Gaussian RBF field (eye landmarks and far regions pinned to zero), damp it locally where it would flip faces (deflip), shorten the step by
     bisection until the existing gates hold (blink gap / lid penetration vs the start mesh, lip gap, ring ratios, no new flips, 3D skin RMS below the cap), re-render and measure;
  4. keep the step only if the true (detector) ratio cost fell, otherwise halve the step and re-plan; the best mesh is written as a candidate tag that r6_12_refit_accept judges like the slider re-fit (IoU and 3D skin RMS may not regress).
Keys follow the basis by delta transfer (keys = template keys + (P - template basis)), as in every other stage.
usage: r6_13_polish.py <base_tag> <cand_tag> [--iters 10] [--sigma 0.005] [--mu 0.5] [--maxmm 1.5] [--skincap 0.94]"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_TGT = _P_RUN + 'target/'; _P_FIT = _P_RUN + 'fit/'; _P_BAKE = _P_RUN + 'bake/'; _P_OUT = _P_RUN + 'out/'
_P_TPLROOT = _os.environ['SCP_TPL_ROOT'].rstrip('/') + '/'; _P_TPL0 = _os.environ['SCP_TPL_NPZ']; _P_SLIDERS = _os.environ['SCP_SLIDERS_DIR'].rstrip('/') + '/'
_P_STAGES = _os.path.dirname(_os.path.abspath(__file__)).replace(chr(92), '/') + '/'; _P_REPO = _os.environ['SCP_REPO'].rstrip('/') + '/'; _P_GLB = _os.environ.get('SCP_TARGET_GLB', _P_TGT + 'shape.glb')
import sys, subprocess, json, time, shutil, argparse
import numpy as np
pre = argparse.ArgumentParser(); pre.add_argument('base'); pre.add_argument('cand'); pre.add_argument('--iters', type=int, default=10); pre.add_argument('--sigma', type=float, default=0.005)
pre.add_argument('--mu', type=float, default=0.5); pre.add_argument('--maxmm', type=float, default=1.5); pre.add_argument('--skincap', type=float, default=0.94); pre.add_argument('--lam', type=float, default=1e-3); pre.add_argument('--minimax', type=float, default=0.0)
pa = pre.parse_args(); F_ = _P_FIT
sys.argv = [sys.argv[0], F_ + f'wrap_r{pa.base}_basis.npy', F_ + f'lm_{pa.base}.json', F_ + f'_cam_{pa.base}.json', F_ + f'rn_{pa.base}.png', pa.cand]
import r6_02_warp as W          # photo ratios, template landmark hits (geo), gates, flips, 3D metrics; its P0 is the base mesh
from r6_common import *
import fit_checks6 as FC
import r6_metrics as MT
from scipy.optimize import least_squares
t = W.t; SC = _P_STAGES; P0 = W.P0.copy(); keys = list(W.photo_r)
EYE_FIX = [33, 263, 133, 362, 159, 145, 386, 374]
MOVE = [129, 358, 61, 291, 234, 454, 172, 397, 105, 334, 2, 13, 152, 10, 0, 17]
assert all(i in W.lpos for i in EYE_FIX + MOVE), 'landmark hits missing'
EV0 = np.concatenate([t.eyeL, t.eyeR]).astype(float); EF = np.concatenate([t.eyeL_f, t.eyeR_f + 512]); ANC = {s: FC.eye_anchor(t, s) for s in ('L', 'R')}
def eyes_for(P):   # eyeballs follow the lower lid ring (as r6_prep_render / r6_06 measure)
    ev = EV0.copy()
    for k, s in enumerate(('L', 'R')):
        ring = ANC[s]; a, b = t.basis[ring], P[ring]; sl = slice(512 * k, 512 * (k + 1)); c0 = ev[sl].mean(0)
        sc = np.linalg.norm(b - b.mean(0), axis=1).mean() / np.linalg.norm(a - a.mean(0), axis=1).mean()
        ev[sl] = c0 + sc * (ev[sl] - c0) + (b.mean(0) - a.mean(0))
    return ev
work = F_ + f'_pol_{pa.cand}'
def measure(P):   # detector landmarks (468, 2) in mm with y = -z (the convention of W.ratios) on the render of P
    f = work + '_m.npz'; np.savez(f, v=P, f=t.faces, ev=eyes_for(P), ef=EF)
    r = subprocess.run([BLENDER, '-b', '-P', SC + 'r6_01b_batch_blender.py', '--', '1.46', '1.92', work + '_i', f], capture_output=True, text=True); assert 'OK' in r.stdout, r.stdout[-400:] + r.stderr[-400:]
    subprocess.run([MP, SC + 'r6_01c_batch_lm.py', work + '_lm.json', work + '_i0.png'], check=True, capture_output=True, env=dict(_os.environ, SCP_LM_JIT='5'))
    rp = np.array(json.load(open(work + '_lm.json'))[work + '_i0.png']); xz = W.px2mm(rp); return np.c_[xz[:, 0], -xz[:, 1]]
def cost_of(L):
    r = W.ratios(L); e = {k: 100 * (r[k] / W.photo_r[k] - 1) for k in keys}; return sum(W.EXTRA_W.get(k, 1.0) ** 2 * v * v for k, v in e.items()), e
# ---- feasibility (same rules as r6_06 feasible(), relative to the start mesh)
PENTOL = 0.45; _G0 = W.gates(P0); _F0 = W.flips_of(P0)
WHY = {}
def feasible(P):
    G = W.gates(P); WHY.clear()
    if MT.tgt_stats(P)['skin_rms'] > SKINCAP: WHY['skin'] = MT.tgt_stats(P)['skin_rms']
    for k, v in G.items():
        if k.startswith('blink_'):
            b0 = _G0[k]
            if v['pen_moved_mm'] > max(v['pen_template_mm'] + PENTOL, b0['pen_moved_mm']) + 0.03: WHY[k + '_pen'] = v['pen_moved_mm']; return False
            if v['gap_max_mm'] > max(v['tpl_gap_max_mm'] + 0.5, b0['gap_max_mm']) + 0.03: WHY[k + '_gapmax'] = v['gap_max_mm']; return False
            if v['gap_mean_mm'] > max(v['tpl_gap_mean_mm'] + 0.5, b0['gap_mean_mm']) + 0.03: WHY[k + '_gapmean'] = v['gap_mean_mm']; return False
        if k.startswith('lip_') and v['min_gap_mm'] < -0.3: return False
    bad = {k: (round(v['min_ratio'], 2), round(v['max_ratio'], 2), v['flips']) for k, v in G['rings'].items() if v['min_ratio'] < 0.7 or v['max_ratio'] > 1.4 or v['flips'] > 0}
    if bad: WHY['rings'] = bad; return False
    if W.flips_of(P) > _F0: WHY['flips'] = W.flips_of(P); return False
    return MT.tgt_stats(P)['skin_rms'] <= SKINCAP
SKINCAP = max(pa.skincap, MT.tgt_stats(P0)['skin_rms'] * 1.0); print('start skin_rms %.3f cap %.3f' % (MT.tgt_stats(P0)['skin_rms'], SKINCAP))    # never above the start value when that is already over the cap
# ---- displacement field: Gaussian RBF over the ratio landmarks (target displacement) and the eye landmarks (zero)
def field(P, delta):   # delta: {landmark: (dx, dz) mm}; returns (n,3) displacement in m (x, z only; y untouched)
    G = W.geo(P); ids = list(delta) + EYE_FIX; S = G[[W.lpos[i] for i in ids]]
    V = np.zeros((len(ids), 3))
    for j, i in enumerate(delta): V[j, 0] = delta[i][0] * 1e-3; V[j, 2] = delta[i][1] * 1e-3
    K = np.exp(-((S[:, None] - S[None]) ** 2).sum(2) / (2 * pa.sigma ** 2)); w = np.linalg.solve(K + pa.lam * np.eye(len(ids)), V)
    Kv = np.exp(-((P[:, None, :] - S[None]) ** 2).sum(2) / (2 * pa.sigma ** 2)); D = Kv @ w
    return D[t.rep]      # welded copies share positions, so they share the displacement
def sens3d(P):   # per (landmark, axis) first / second derivative of the skin mean-square distance to the 3D target (mm^2 per mm, mm^2 per mm^2), from +-eps probes of the RBF field
    S0 = MT.tgt_stats(P)['skin_rms'] ** 2; eps = 1.5; g = np.zeros(2 * len(MOVE)); h = np.zeros(2 * len(MOVE))
    for a in range(2):
        for j, i in enumerate(MOVE):
            v = []
            for sg in (1, -1):
                d = {i: (sg * eps * (a == 0), sg * eps * (a == 1))}; v.append(MT.tgt_stats(deflip(t, P, P + field(P, d)))['skin_rms'] ** 2)
            g[a * len(MOVE) + j] = (v[0] - v[1]) / (2 * eps); h[a * len(MOVE) + j] = (v[0] + v[1] - 2 * S0) / eps ** 2
    return S0, g, h
def plan(L, P):   # smallest landmark displacement (mm) that brings the 13 ratios to the photo values while the predicted 3D skin error stays under the cap
    n = len(MOVE); S0, g, h = sens3d(P); cap2 = SKINCAP ** 2; cur_r = W.ratios(L)      # minimax > 0: ratios that are far off carry more weight (iteratively re-weighted); off by default (no gain on Cloud)
    def Lof(x):
        M = L.copy(); M[MOVE, 0] += x[:n]; M[MOVE, 1] -= x[n:]; return M
    def res(x):
        r = W.ratios(Lof(x)); Sp = S0 + g @ x + 0.5 * (h * x * x).sum()
        return np.r_[[W.EXTRA_W.get(k, 1.0) * 100 * (r[k] / W.photo_r[k] - 1) * (1 + pa.minimax * abs(100 * (cur_r[k] / W.photo_r[k] - 1)) / 5) ** 0.5 for k in keys], pa.mu * x, 80 * max(0.0, Sp - 0.97 * cap2) / cap2]
    s = least_squares(res, np.zeros(2 * n), bounds=(-pa.maxmm * np.ones(2 * n), pa.maxmm * np.ones(2 * n)), diff_step=1e-3)
    return {i: (float(s.x[j]), float(s.x[n + j])) for j, i in enumerate(MOVE)}
def apply(P, delta, s):
    d = {i: (s * v[0], s * v[1]) for i, v in delta.items()}; return deflip(t, P, P + field(P, d))
P = P0.copy(); L = measure(P); c0, e0 = cost_of(L); best = (c0, P.copy(), e0); cur_c = c0
print('polish start: cost %.1f, MAE(11) %.2f, worst %.1f' % (c0, np.mean([abs(e0[k]) for k in keys[:11]]), max(abs(e0[k]) for k in keys[:11])), {k: round(v, 1) for k, v in e0.items() if abs(v) > 2}, flush=True)
step = 1.0; acc = 0; tries = 0
while acc < pa.iters and tries < pa.iters * 2 + 2 and step > 0.1:
    tries += 1; t0 = time.time(); dl = plan(L, P)
    Pt = apply(P, dl, step)
    if not feasible(Pt):      # shorten along the step until the gates / 3D cap hold
        feasible(Pt); why0 = dict(WHY); lo, hi = 0.0, step
        for _ in range(7):
            m = (lo + hi) / 2
            if feasible(apply(P, dl, m)): lo = m
            else: hi = m
        print('  gates/cap: step shortened %.2f -> %.2f; first failure at full step:' % (step, lo), why0, flush=True); step = lo
        if step < 0.1: break
        Pt = apply(P, dl, step)
    Lt = measure(Pt); ct, et = cost_of(Lt)
    if _os.environ.get('SCP_POLISH_DEBUG'):   # requested vs achieved detector motion (mm; x right, z up) per landmark
        for i in MOVE: print('   lm %3d req (%+.2f, %+.2f) got (%+.2f, %+.2f)' % (i, step * dl[i][0], step * dl[i][1], Lt[i, 0] - L[i, 0], -(Lt[i, 1] - L[i, 1])))
    print('try %d step %.2f: cost %.1f -> %.1f  MAE(11) %.2f worst %.1f  (%.0fs)' % (tries, step, cur_c, ct, np.mean([abs(et[k]) for k in keys[:11]]), max(abs(et[k]) for k in keys[:11]), time.time() - t0),
          {k: round(v, 1) for k, v in et.items() if abs(v) > 2}, flush=True)
    if ct < cur_c * 0.98:
        P, L, cur_c = Pt, Lt, ct; acc += 1
        if ct < best[0]: best = (ct, P.copy(), et)
    else: step *= 0.5
c, P, e = best
print('POLISH best cost %.1f (start %.1f), accepted steps %d' % (c, c0, acc))
chk = W.gates(P); fl = W.flips_of(P); st = MT.tgt_stats(P); print('3D', st, 'GATES PASS=%s flips %d' % (chk['PASS'], fl))
dB = P - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = P
tg = pa.cand; np.save(F_ + f'wrap_r{tg}_basis.npy', P.astype(np.float32)); np.save(F_ + f'wrap_r{tg}_keys.npy', nk); np.savez(F_ + f'wrap_r{tg}_mesh.npz', v=P, f=t.faces)
json.dump(dict(theta={}, ratio_err_pct=e, checks=chk, target=st, flips=fl, polish=dict(start_cost=c0, cost=c, accepted=acc, args=vars(pa))), open(F_ + f'warp_{tg}.json', 'w'), indent=1, default=float)
rep = json.load(open(F_ + f'wrap_r{pa.base}_report.json')); json.dump(rep, open(F_ + f'wrap_r{tg}_report.json', 'w'), indent=1, default=float)
print('POLISH DONE')
