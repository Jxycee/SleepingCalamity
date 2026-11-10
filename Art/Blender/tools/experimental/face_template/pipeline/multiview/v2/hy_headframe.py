"""Hunyuan mesh -> pipeline head frame. The pipeline's multiview target expects the TRELLIS head-frame convention (head + neck cut ~1.15 H under the crown, unit scale of mv_silhouette_iou).
usage: hy_headframe.py <hy_headframe.npz (from mv_silhouette_iou.py)> <ref_headframe.npz (TRELLIS fblr)> <out.npz>
Scale s from matching the x-extent profile of the first 55 % of the reference height below the crown, then the bust is clipped at the reference height; recentred like the reference."""
import sys, numpy as np
h = np.load(sys.argv[1]); r = np.load(sys.argv[2]); hv, hf = h['v'].astype(float), h['f'].astype(int); rv = r['v'].astype(float)
def prof(v, ds, tol):   # x half-extent of vertices within tol of depth d below the crown
    zt = v[:, 2].max(); out = []
    for d in ds:
        m = np.abs((zt - v[:, 2]) - d) < tol; out.append(np.ptp(v[m, 0]) if m.sum() > 20 else np.nan)
    return np.array(out)
HT = np.ptp(rv[:, 2]); ds = np.linspace(0.05, 0.55, 26) * HT; tol = 0.012 * HT; pr = prof(rv, ds, tol)
best = None
for s in np.linspace(0.08, 1.2, 561):
    ph = s * prof(hv, ds / s, tol / s); e = np.nanmean(np.abs(ph - pr))
    if best is None or e < best[0]: best = (e, s)
e, s = best; v = hv * s; zt = v[:, 2].max(); keep = (v[hf, 2] > zt - HT).all(1); f = hf[keep]
used = np.unique(f); remap = -np.ones(len(v), int); remap[used] = np.arange(len(used)); v, f = v[used], remap[f]
v[:, 0] -= 0.5 * (v[:, 0].min() + v[:, 0].max()); v[:, 1] -= v[:, 1].mean() - rv[:, 1].mean() + 0; v[:, 2] += rv[:, 2].max() - v[:, 2].max()
print('scale %.4f (profile err %.4f ref units), ref height %.3f, clipped faces %d -> %d, depth(y) ref %.3f hy %.3f, width ref %.3f hy %.3f' % (s, e, HT, len(hf), len(f), np.ptp(rv[:, 1]), np.ptp(v[:, 1]), np.ptp(rv[:, 0]), np.ptp(v[:, 0])))
np.savez(sys.argv[3], v=v.astype(np.float32), f=f.astype(np.int32))
