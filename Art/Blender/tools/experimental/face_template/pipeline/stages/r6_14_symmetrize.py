"""Round 14 SYMMETRIZE (optional snap sub-step, after the polish). Venv python.
Game faces are near-symmetric, the template is symmetric about x = 0, and the detector / photo cannot see the ears (or a slightly yawed side) well, so left and right come out different sizes.
This step makes the displacement field d = P - template basis mirror-symmetric:
  mirror map  m(v) = welded vertex nearest to the x-mirrored basis position (template basis is symmetric to a median 0.9 mm; the map is checked: round-trip m(m(v)) = v for >= 90 % of the welded vertices);
  mode avg    d_sym(v) = 0.5 * (d(v) + M d(m(v)))   with M = diag(-1, 1, 1)  (ears: the mean ear size and position; midline vertices keep x = 0 because dx(v) = -dx(m(v)) = 0 there)
  mode better d_sym(v) = d(v) on the better half (smaller 3D skin distance to the Hunyuan target on that half), mirrored onto the other half; midline blended over 5 mm.
The silhouette / ratio judgement is the caller's (r6_12_refit_accept, label symmetrize); this script also reports the left/right ear size (vertex z/y extent of the |x| > ear_x set) before and after.
The photo yaw does not enter: the field is relative to the template head frame, not the image; the yawed photo only reaches the fit through the detector landmarks, so averaging the halves is what removes the yaw-induced asymmetry.
usage: r6_14_symmetrize.py <base_tag> <cand_tag> [--mode avg|better] [--earx 0.082]"""
import os as _os
_P_RUN = _os.environ['SCP_RUN'].rstrip('/') + '/'; _P_FIT = _P_RUN + 'fit/'
import sys, json, argparse
import numpy as np
from scipy.spatial import cKDTree
pre = argparse.ArgumentParser(); pre.add_argument('base'); pre.add_argument('cand'); pre.add_argument('--mode', default='avg'); pre.add_argument('--earx', type=float, default=0.082)
pa = pre.parse_args(); F_ = _P_FIT
sys.argv = [sys.argv[0], F_ + f'wrap_r{pa.base}_basis.npy', F_ + f'lm_{pa.base}.json', F_ + f'_cam_{pa.base}.json', F_ + f'rn_{pa.base}.png', pa.cand]
import r6_02_warp as W
from r6_common import *
import r6_metrics as MT
from fit_common import repair_flips, count_flips
t = W.t; P0 = W.P0.copy(); nW = int(t.rep.max()) + 1
first = np.full(nW, -1, int)
for v in range(len(t.rep) - 1, -1, -1): first[t.rep[v]] = v           # one representative vertex per welded vertex
BW = t.basis[first]; M_ = BW.copy(); M_[:, 0] = -M_[:, 0]
dist, mw = cKDTree(BW).query(M_); rt = (mw[mw] == np.arange(nW)).mean()
print('mirror map: median %.2f mm, p99 %.2f mm, round trip %.1f %%' % (np.median(dist) * 1e3, np.percentile(dist, 99) * 1e3, 100 * rt))
assert rt >= 0.8, 'mirror map round trip %.2f' % rt
def ear_metric(P):
    PW = P[first]; out = {}
    for s_, sel in (('L', BW[:, 0] > pa.earx), ('R', BW[:, 0] < -pa.earx)):
        if sel.sum() < 10: out[s_] = None; continue
        Q = PW[sel]; out[s_] = dict(n=int(sel.sum()), height_mm=float((Q[:, 2].max() - Q[:, 2].min()) * 1e3), depth_mm=float((Q[:, 1].max() - Q[:, 1].min()) * 1e3), top_z=float(Q[:, 2].max()), bottom_z=float(Q[:, 2].min()), x_out=float(np.abs(Q[:, 0]).max()))
    if out['L'] and out['R']: out['height_ratio_LR'] = out['L']['height_mm'] / out['R']['height_mm']; out['top_dz_mm'] = (out['L']['top_z'] - out['R']['top_z']) * 1e3
    return out
dW = (P0 - t.basis)[first]; Md = dW[mw] * np.array([-1.0, 1.0, 1.0])
if pa.mode == 'avg': ds = 0.5 * (dW + Md)
else:
    hs = np.zeros(nW)
    try:
        import r6_metrics as _M; per = _M.tgt_stats(P0, per_vertex=True)['per_vertex']      # per-vertex skin distance to the 3D target when the metric offers it
        hs = np.asarray(per)[first]
    except Exception: pass
    left = BW[:, 0] > 0; eL = hs[left].mean() if hs[left].size else 0; eR = hs[~left].mean() if hs[~left].size else 0; good = left if eL <= eR else ~left
    wgt = np.clip((np.abs(BW[:, 0]) / 0.005), 0, 1); on_good = np.where(good, 1.0, 0.0); wgt_good = on_good + (1 - on_good) * 0.0
    pick = np.where(good[:, None], dW, Md); ds = wgt[:, None] * pick + (1 - wgt[:, None]) * 0.5 * (dW + Md)
Ps = t.basis.copy() + 0.0; Ps = Ps + ds[t.rep]
Ps, f0, f1 = repair_flips(t, Ps)
print('flips after symmetrize %d -> %d; max change %.2f mm, mean %.2f mm' % (f0, f1, np.abs(Ps - P0).max() * 1e3, np.linalg.norm(Ps - P0, axis=1).mean() * 1e3))
em0, em1 = ear_metric(P0), ear_metric(Ps); print('ear L/R height ratio %.3f -> %.3f (top dz %.2f -> %.2f mm)' % (em0.get('height_ratio_LR', float('nan')), em1.get('height_ratio_LR', float('nan')), em0.get('top_dz_mm', float('nan')), em1.get('top_dz_mm', float('nan'))))
chk = W.gates(Ps); fl = W.flips_of(Ps); st = MT.tgt_stats(Ps); print('3D', st, 'GATES PASS=%s flips %d' % (chk['PASS'], fl))
dB = Ps - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = Ps
tg = pa.cand; np.save(F_ + f'wrap_r{tg}_basis.npy', Ps.astype(np.float32)); np.save(F_ + f'wrap_r{tg}_keys.npy', nk); np.savez(F_ + f'wrap_r{tg}_mesh.npz', v=Ps, f=t.faces)
json.dump(dict(theta={}, checks=chk, target=st, flips=fl, symmetrize=dict(mode=pa.mode, ears_before=em0, ears_after=em1, mirror_roundtrip=float(rt))), open(F_ + f'warp_{tg}.json', 'w'), indent=1, default=float)
rep = json.load(open(F_ + f'wrap_r{pa.base}_report.json')); json.dump(rep, open(F_ + f'wrap_r{tg}_report.json', 'w'), indent=1, default=float)
json.dump(dict(before=em0, after=em1), open(F_ + 'ear_metric.json', 'w'), indent=1)
print('SYMMETRIZE DONE')
