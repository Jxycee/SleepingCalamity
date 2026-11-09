"""Round 16 SKULL DEPTH (snap sub-step, after the skull-width passes and before the ratio re-fit / polish). Venv python.
The width step (r6_04) only sees the FRONT view; the template cranium is cut off at the back of the head and the photo says nothing about it, so from the side the back of the skull is a near-vertical line
inside a real head's outline (vision/SIDE_README.md). Two smooth displacement fields, both measured against the SDXL side-profile prior (vision/side_prior.json), head vertices only:
 1. (mode 'y', default) y-only back-contour field: the back of the skull moves backwards (depth only; z and x untouched, so the front silhouette and the width step's dome are exactly preserved) towards the prior's back-contour median, see back_field().
    (mode 'radial', kept for comparison; it also moves z and cost 2-3 % front skull IoU) sagittal radial field: pivot P = (nose tip + 0.5 H behind, crown - 0.5 H) in the (y, z) plane (H = crown-to-menton of the current side silhouette); theta = angle about P (0 = straight back, 90 = up,
    180 = front), r(theta) = the mesh's smoothed outline radius, target = the prior median radius; s(theta) = clip(1 + alpha (r_prior / r_mesh - 1), 0.85, 1.15). Vertex (y, z) moves radially by (s - 1) * w * r,
    x is untouched. w = smoothstep(u, .22, .45) * (1 - smoothstep(v, .55, .80)) * smoothstep(theta, -35, -20) * (1 - smoothstep(theta, 165, 180)): the face and forehead (u < .22), the jaw and neck
    (v > .8) and everything outside the cranium are pinned (u, v = depth behind the nose tip / drop below the crown, in H).
 2. crown-cap widening (cap_y, cap_x): the top ~12 % of H: y (depth) and x (width) scaled about the cap centre by 1 + beta * bump(v), beta solved (bisection, <= cap_max) so the side-view cap width
    dome_round reaches `cap_target_z` sigma of the prior median; x widening is skipped for sides that are already wide enough.
The silhouette is rasterised from the mesh itself (cv2, same fixed clay frame as clay_views.py: 1 px = 0.486 mm), so the stage needs no Blender render and `score_final` can score the side joint distance.
usage: r6_16_skulldepth.py <tag>   (reads/writes fit/wrap_r<tag>_{basis,keys,mesh}; config snap.skulldepth {enabled, alpha, cap_y, cap_x, cap_target_z, cap_max}; writes fit/skulldepth.json)"""
import os, sys, json, numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
sys.path.insert(0, HERE + '/../vision')
import side_prior as SP
CZ, S2, R2, CY = 1.70, 0.34, 700, -0.02            # fixed clay frame (clay_views.py): centre y -0.02, z 1.70, 0.34 m over 700 px
_PRIOR = None
def prior():
    global _PRIOR
    if _PRIOR is None: _PRIOR = json.load(open(SP.PRIOR))
    return _PRIOR
def sstep(x, a, b):
    t = np.clip((np.asarray(x, float) - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)
def silhouette(P, F):
    """side silhouette (image right = +y = back; head faces left) of mesh P, faces F, in the clay frame"""
    px = (P[:, 1] - CY) / S2 * R2 + R2 / 2; py = (0.5 - (P[:, 2] - CZ) / S2) * R2
    tri = np.stack([px, py], 1)[np.asarray(F)[:, :3]].astype(np.float32)
    m = np.zeros((R2, R2), np.uint8)
    for t in np.round(tri * 16).astype(np.int32): cv2.fillConvexPoly(m, t, 255, lineType=cv2.LINE_8, shift=4)      # one call per triangle (a single fillPoly call XORs overlaps)
    return m > 127
def crown_taper(P, F, H, zc):
    """front-view skull width at 10 % of H below the crown over the width at 30 % (the plausibility crown_taper feature; prior median 0.7026, sigma 0.0212) from the mesh's front silhouette"""
    px = P[:, 0] / S2 * R2 + R2 / 2; py = (0.5 - (P[:, 2] - CZ) / S2) * R2
    tri = np.stack([px, py], 1)[np.asarray(F)[:, :3]].astype(np.float32); m = np.zeros((R2, R2), np.uint8)
    for t in np.round(tri * 16).astype(np.int32): cv2.fillConvexPoly(m, t, 255, lineType=cv2.LINE_8, shift=4)
    def w(f):
        r = int(round((0.5 - (zc - f * H - CZ) / S2) * R2)); xs = np.where(m[r] > 127)[0]; return float(xs.max() - xs.min() + 1) if len(xs) else np.nan
    return w(0.10) / w(0.30)
TAPER_MED, TAPER_SIG = 0.7026, 0.0212
def measure(P, F):
    return SP.measure(silhouette(P, F))
def frame(m):
    """H, nose-tip y, crown z (metres) of a measure() result"""
    a = m['anchors']; H = m['H_px'] / R2 * S2; return H, (a['nose_x'] / R2 - 0.5) * S2 + CY, CZ + (0.5 - a['top'] / R2) * S2
def joint_distance(m):
    pc = prior()['pca_radial']; x = np.array(m['radial']) - np.array(pc['mean']); return float(np.sqrt(((np.array(pc['comps']) @ x / np.array(pc['std'])) ** 2).sum()))
def z_scalars(m):
    P_ = prior(); return {k: (m['scalars'][k] - P_['features'][k]['median']) / P_['features'][k]['sigma'] for k in SP.FEATS if m['scalars'].get(k) is not None}
def radial_field(P, m, alpha=0.8, smax=0.15, over_k=1.0):
    """sagittal radial displacement (n,3) for vertices P given the current measure m"""
    P_ = prior(); H, yn, zc = frame(m); th = np.array(SP.TH, float)
    rm = np.maximum(np.array(m['radial']), 0.05); rp = np.array(P_['radial']['median'])
    if over_k is not None: rp = np.where(rm > rp + over_k * np.array(P_['radial']['sigma']), rp + over_k * np.array(P_['radial']['sigma']), np.where(rm > rp, rm, rp))   # one-sided: a skull fuller than the median is only pulled back to median + over_k sigma (a deep head is not a cut-off one); a flat one is pulled to the median
    s = np.clip(1 + alpha * (rp / rm - 1), 1 - smax, 1 + smax)
    s = np.convolve(np.pad(s, 2, mode='edge'), np.ones(5) / 5, 'valid')                       # 5-point boxcar over theta
    yp, zp = yn + SP.PIV[0] * H, zc - SP.PIV[1] * H
    dy, dz = P[:, 1] - yp, P[:, 2] - zp; r = np.hypot(dy, dz); tt = np.degrees(np.arctan2(dz, dy)); sv = np.interp(tt, th, s)
    u = (P[:, 1] - yn) / H; v = (zc - P[:, 2]) / H
    w = sstep(u, 0.22, 0.45) * (1 - sstep(v, 0.55, 0.80)) * sstep(tt, -35, -20) * (1 - sstep(tt, 165, 180))
    D = np.zeros_like(P); k = (sv - 1) * w
    D[:, 1] = k * dy; D[:, 2] = k * dz; return D
def back_field(P, m, alpha=0.8, over_k=1.0, dmax=0.07):
    """y-only (depth) displacement of the back of the skull towards the prior's back-contour median. z and x are untouched, so the FRONT silhouette (x, z) cannot change: the width step's dome / IoU stays exactly as fitted.
    Delta(v) = alpha * (target(v) - back_mesh(v)) over the v grid (target = prior median, but a skull fuller than median + over_k sigma is only pulled back to that), smoothed; vertex y += Delta(v(z)) * H * wt(t) * wv(v),
    t = (y - nose tip) / (depth H) (0 nose, 1 occiput): wt = smoothstep(t, .55, .95) (ears and face stay, the cranium behind them moves), wv = smoothstep(v, 0, .05) * (1 - smoothstep(v, .55, .80)) (crown row and neck stay)"""
    P_ = prior(); H, yn, zc = frame(m); vg = np.array(SP.VG, float); bm = np.array([np.nan if x is None else x for x in m['back']], float)
    med = np.array(P_['back']['median'], float); sg = np.array(P_['back']['sigma'], float)
    tgt = np.where(bm > med + over_k * sg, med + over_k * sg, np.where(bm > med, bm, med)); d = alpha * (tgt - bm)
    ok = np.isfinite(d) & (vg <= 0.62); d = np.clip(d, -dmax, dmax)
    v = (zc - P[:, 2]) / H; D = np.zeros_like(P); dv = np.interp(v, vg[ok], d[ok]); depth = m['scalars']['depth']; t = (P[:, 1] - yn) / (depth * H)
    D[:, 1] = dv * H * sstep(t, 0.55, 0.95) * sstep(v, 0.0, 0.05) * (1 - sstep(v, 0.55, 0.80)); return D
def cap_scale(P, m, by, bx):
    """y / x widening of the crown cap: scale about the cap centre by 1 + b * bump(v), bump rises over v .00-.04, falls over v .08-.20 (the cap rows only)"""
    H, yn, zc = frame(m); v = (zc - P[:, 2]) / H
    bump = sstep(v, 0.0, 0.04) * (1 - sstep(v, 0.08, 0.20)); Q = P.copy()
    yc = yn + 0.5 * m['scalars']['depth'] * H                                                  # cap centre: mid of the nose-to-occiput span
    Q[:, 1] = yc + (P[:, 1] - yc) * (1 + by * bump); Q[:, 0] = P[:, 0] * (1 + bx * bump); return Q
def apply_all(P, F, cfg, log=print):
    """returns (Q, info). cfg: alpha, cap_y, cap_x (bool), cap_target_z, cap_max, iters"""
    alpha = float(cfg.get('alpha', 0.8)); info = dict(alpha=alpha)
    m0 = measure(P, F); info['before'] = dict(joint=joint_distance(m0), z=z_scalars(m0))
    Q = P.copy()
    for _ in range(int(cfg.get('iters', 1))):
        m = measure(Q, F)
        if m is None: break
        Q = Q + (radial_field(Q, m, alpha, over_k=cfg.get('over_k', 1.0)) if cfg.get('mode', 'y') == 'radial' else back_field(Q, m, alpha, over_k=cfg.get('over_k', 1.0)))
    m1 = measure(Q, F); info['radial'] = dict(joint=joint_distance(m1), z=z_scalars(m1), max_mm=float(np.linalg.norm(Q - P, axis=1).max() * 1e3))
    if (cfg.get('cap_y', True) or cfg.get('cap_x', True)) and m1 is not None:
        bmax = float(cfg.get('cap_max', 0.08)); H1, _, zc1 = frame(m1)
        def solve(ok, bmax=bmax):      # smallest beta in [0, bmax] for which ok(beta) holds (0 when it already holds, bmax when never)
            if ok(0.0): return 0.0
            lo, hi = 0.0, bmax
            for _ in range(8):
                mid = 0.5 * (lo + hi)
                if ok(mid): hi = mid
                else: lo = mid
            return hi
        zd = lambda mm: z_scalars(mm).get('dome_round', 0.0)
        by = solve(lambda b: (lambda mm: mm is not None and zd(mm) >= float(cfg.get('cap_target_z', -1.5)))(measure(cap_scale(Q, m1, b, 0), F))) if cfg.get('cap_y', True) else 0.0
        # x widening only for a gabled / pointed front crown (crown_taper below the prior median by more than cap_x_z sigma); a crown already round or wide (char2_zero, char4) is left alone: widening it overshot the plausibility crown_taper (+2.0 -> +2.9, +0.7 -> +2.7)
        tz = float(cfg.get('cap_x_z', -1.5)); bx = solve(lambda b: (crown_taper(cap_scale(Q, m1, 0, b), F, H1, zc1) - TAPER_MED) / TAPER_SIG >= tz, float(cfg.get('cap_max_x', 0.04))) if cfg.get('cap_x', True) else 0.0
        info['taper_z'] = [(crown_taper(Q, F, H1, zc1) - TAPER_MED) / TAPER_SIG]
        Q = cap_scale(Q, m1, by, bx); info['cap'] = dict(by=by, bx=bx); info['taper_z'].append((crown_taper(Q, F, H1, zc1) - TAPER_MED) / TAPER_SIG)
    m2 = measure(Q, F); info['after'] = dict(joint=joint_distance(m2), z=z_scalars(m2), max_mm=float(np.linalg.norm(Q - P, axis=1).max() * 1e3), depth=m2['scalars']['depth'], H_mm=m2['H_px'] / R2 * S2 * 1e3)
    return Q, info
def main():
    from r6_common import Tpl, FIT
    from fit_common import repair_flips, count_flips
    tag = sys.argv[1]; t = Tpl(); P0 = np.load(FIT + f'wrap_r{tag}_basis.npy').astype(np.float64)
    cfgp = json.load(open(os.environ['SCP_RUN'].rstrip('/') + '/config.resolved.json')); cfg = (cfgp.get('snap') or {}).get('skulldepth') or {}
    Q, info = apply_all(P0, t.faces, cfg)
    Q, f0, f1 = repair_flips(t, Q); info['flips'] = [int(f0), int(f1)]
    ok = f1 == 0 and info['after']['joint'] <= info['before']['joint'] + 0.05 and info['after']['max_mm'] <= float(cfg.get('max_mm', 16))
    print('skull depth: joint %.2f -> radial %.2f -> final %.2f, max move %.1f mm, flips %d -> %d, cap %s, depth %.3f H -> %s' % (info['before']['joint'], info['radial']['joint'], info['after']['joint'], info['after']['max_mm'], f0, f1, info.get('cap'), info['after']['depth'], 'APPLIED' if ok else 'skipped'))
    info['applied'] = bool(ok)
    if ok:
        dB = Q - t.basis; nk = (t.keys + dB[None]).astype(np.float32); nk[0] = Q
        np.save(FIT + f'wrap_r{tag}_basis.npy', Q.astype(np.float32)); np.save(FIT + f'wrap_r{tag}_keys.npy', nk); np.savez(FIT + f'wrap_r{tag}_mesh.npz', v=Q, f=t.faces)
        try:
            import r6_metrics as MT
            rp = json.load(open(FIT + f'wrap_r{tag}_report.json')); rp['target'] = MT.tgt_stats(Q); json.dump(rp, open(FIT + f'wrap_r{tag}_report.json', 'w'), indent=1, default=float); print('3D target after skull depth', rp['target'])
        except Exception as ex: print('report 3D stats not updated:', ex)
    json.dump(info, open(FIT + 'skulldepth.json', 'w'), indent=1, default=float)
    print('SKULLDEPTH DONE')
if __name__ == '__main__': main()
