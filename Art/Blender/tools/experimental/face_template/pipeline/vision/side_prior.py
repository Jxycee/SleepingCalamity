"""Side-profile skull prior: silhouettes of SDXL bald side portraits -> normalised profile curves + scalar features -> side_prior.json.
usage (facekeys python):  python side_prior.py build [--faces C:/AI/test_out/heroes/vision_prior/side] [--qc out_dir]
Library: segment(bgr) -> bool mask, measure(mask_facing_left) -> dict (scalars + curves), used by side_profile_metric.py for the mesh clay side render with the SAME code.

Frame (every silhouette, photo or clay, mirrored to face LEFT like the clay side render): H = crown-to-menton height of the silhouette (menton = bottom of the chin, found
from the front contour); u = (x - x_nose)/H, positive BACKWARDS from the nose tip; v = (y - crown)/H, positive DOWN. So 'u 0.95' is the back of the skull for a head with 0.95 H nose-to-occiput depth.
Anchors are silhouette-only (nose tip, crown, menton): the ear/porion is NOT used because the ear is not in the silhouette and the mesh ears are a separate asset.
Curves: back(v) / front(v) = silhouette extent at fixed v (crown..neck), radial r(theta) from the fixed pivot (u 0.50, v 0.50) in H units
(theta 0 = straight back, 90 = up, 180 = front). Scalars: see FEATS. Honest limits in SIDE_README.md."""
import os, sys, json, argparse, glob, numpy as np, cv2
from scipy import ndimage as ndi
HERE = os.path.dirname(os.path.abspath(__file__)).replace(chr(92), '/')
PRIOR = HERE + '/side_prior.json'
VG = [0.03, 0.06, 0.10, 0.15, 0.20, 0.30, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.00, 1.10]      # v grid for back/front extents
TH = list(range(-20, 166, 5))                                                                   # theta grid (deg) for the radial profile
PIV = (0.50, 0.50)                                                                              # pivot (u, v) in H units
FEATS = ['depth', 'occ_v', 'crown_frac', 'crown_u', 'back_flat_hi', 'back_flat_lo', 'dome_round', 'forehead_slope', 'nose_prot', 'nape_inset', 'neck_depth', 'throat_back']
WORDS = {   # feature -> (plain meaning when z is positive, when negative)
    'depth': ('head too deep front-to-back (nose tip to back of skull)', 'head too shallow front-to-back (back of the skull cut off)'),
    'occ_v': ('the back of the skull bulges too low', 'the back of the skull bulges too high'),
    'crown_frac': ('the crown sits too far back', 'the crown sits too far forward'),
    'crown_u': ('the crown top is too far behind the nose', 'the crown top is too far forward of the nose'),
    'back_flat_hi': ('upper back of the skull too full (bulges out high)', 'upper back of the skull too flat / sloped away'),
    'back_flat_lo': ('lower back of the skull (occiput) too full', 'lower back of the skull (occiput) too flat / tucked in'),
    'dome_round': ('crown cap too wide / flat-topped (cap width at 6 % of H below the crown)', 'crown cap too narrow / pointed (gabled) at the top'),
    'forehead_slope': ('forehead slopes back too much', 'forehead too vertical / bulging'),
    'nose_prot': ('nose projects too far', 'nose projects too little'),
    'nape_inset': ('neck sits too far forward of the skull (back of skull overhangs the neck too much)', 'neck sits too far back (no occipital overhang)'),
    'neck_slope': ('back of the neck leans forward too much', 'back of the neck leans backward / slopes away too much'),
    'neck_depth': ('neck too thick front-to-back', 'neck too thin front-to-back'),
    'throat_back': ('under-chin/throat line sits too far back (long jaw to neck)', 'throat sits too far forward (short jaw, neck hidden under the chin)'),
}


def segment(bgr, work=512, thr=9.0):
    """plain-backdrop photo -> head+neck+shirt foreground mask (full resolution). Background = quadratic Lab surface fitted to the top and side border strips (robust, 2 passes); foreground = Lab distance > thr;
    closed, holes filled, largest component. (GrabCut cut through pale / stubbled scalps, so it is not used.)"""
    h, w = bgr.shape[:2]; s = work / max(h, w); sm = cv2.resize(bgr, (int(w * s), int(h * s)), interpolation=cv2.INTER_AREA); H, W = sm.shape[:2]
    lab = cv2.cvtColor(cv2.GaussianBlur(sm, (0, 0), 1.5), cv2.COLOR_BGR2LAB).astype(np.float32); yy, xx = np.mgrid[0:H, 0:W]; xn, yn = xx / W - 0.5, yy / H - 0.5
    A = np.stack([np.ones_like(xn), xn, yn, xn * xn, xn * yn, yn * yn], -1).reshape(-1, 6); L = lab.reshape(-1, 3)
    sel = np.zeros((H, W), bool); sel[:int(0.06 * H)] = True; sel[:, :int(0.04 * W)] = True; sel[:, W - int(0.04 * W):] = True; sel = sel.ravel()
    use = sel.copy()
    for _ in range(3):
        coef = np.linalg.lstsq(A[use], L[use], rcond=None)[0]; d = np.linalg.norm(A @ coef - L, axis=1); use = sel & (d < max(2.0 * np.median(d[sel]), 4.0))
    dist = np.linalg.norm(A @ coef - L, axis=1).reshape(H, W); fg = dist > thr
    fg = ndi.binary_closing(fg, iterations=4); fg = ndi.binary_opening(fg, iterations=2); fg = ndi.binary_fill_holes(fg); lab_, n = ndi.label(fg)
    if n == 0: return np.zeros((h, w), bool)
    sizes = ndi.sum(fg, lab_, range(1, n + 1)); fg = lab_ == (1 + int(np.argmax(sizes)))
    return cv2.resize(fg.astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST).astype(bool)


def _front_back(mask):
    rows = np.where(mask.any(1))[0]; xf = np.full(mask.shape[0], np.nan); xb = np.full(mask.shape[0], np.nan)
    for y in rows:
        xs = np.where(mask[y])[0]; xf[y], xb[y] = xs.min(), xs.max()
    return xf, xb, rows


def anchors(mask):
    """nose tip, crown, menton of a LEFT-facing silhouette. Returns dict or None."""
    xf, xb, rows = _front_back(mask)
    if len(rows) < 50: return None
    top = int(rows[0]); Bh = rows[-1] - top; band = slice(top + 5, top + max(int(0.25 * Bh), 10))
    D0 = np.nanmax(xb[band]) - np.nanmin(xf[band]); H0 = D0 / 0.78            # first guess of crown-to-menton from the cranial depth near the top
    r0, r1 = int(top + 0.40 * H0), int(top + 0.70 * H0); r1 = min(r1, rows[-1])
    if r1 <= r0: return None
    rn = r0 + int(np.nanargmin(xf[r0:r1 + 1])); xn = xf[rn]
    c0, c1 = int(rn + 0.24 * H0), min(int(rn + 0.58 * H0), rows[-1])
    if c1 <= c0: return None
    rp = c0 + int(np.nanargmin(xf[c0:c1 + 1])); xp = xf[rp]                       # pogonion: most forward chin point
    rm = rp
    for r in range(rp, min(int(rp + 0.45 * H0), int(rows[-1]))):                  # menton: where the front contour jumps back under the chin
        if xf[r] - xp > 0.10 * H0: break
        rm = r
    H = rm - top
    if H < 80: return None
    return dict(top=top, nose_row=rn, nose_x=float(xn), pog_row=rp, menton_row=int(rm), H=float(H), xf=xf, xb=xb)


def _interp(arr, y):
    y0 = int(np.floor(y)); y1 = min(y0 + 1, len(arr) - 1); t = y - y0
    a, b = arr[y0], arr[y1]
    return a * (1 - t) + b * t


def measure(mask):
    """scalars + curves of a LEFT-facing silhouette; None when the anchors fail. Units: H (crown-to-menton)."""
    A = anchors(mask)
    if A is None: return None
    H, top, xn, xf, xb = A['H'], A['top'], A['nose_x'], A['xf'], A['xb']; nrow = mask.shape[0]
    U = lambda x: (x - xn) / H
    rv = lambda v: top + v * H
    ok = lambda v: 0 <= rv(v) < nrow - 1 and not np.isnan(xb[int(rv(v))]) and not np.isnan(xb[int(rv(v)) + 1])
    back = {v: (U(_interp(xb, rv(v))) if ok(v) else None) for v in VG}; front = {v: (U(_interp(xf, rv(v))) if ok(v) else None) for v in VG}
    vs = np.arange(0.04, 0.66, 0.005); ub = np.array([U(_interp(xb, rv(v))) if ok(v) else np.nan for v in vs]); i = int(np.nanargmax(ub))
    near = vs[ub >= ub[i] - 0.004]; occ_v = float(np.median(near)); depth = float(ub[i])
    crown_u = float(U(np.mean(np.where(mask[top + 2])[0]))); nose_v = (A['nose_row'] - top) / H
    f_hi = front[0.15]; f_lo = front[0.30]
    # back flatness: how far the back contour sits inside the occiput line at fixed v (positive = the skull is fuller)
    bf_hi = back[0.20] - depth if back[0.20] is not None else None; bf_lo = back[0.50] - depth if back[0.50] is not None else None
    # dome roundness: crown-row half-width style measure = extent (back - front) at v 0.06 over the depth
    dome = (back[0.06] - front[0.06]) / max(depth, 1e-6) if back[0.06] is not None and front[0.06] is not None else None
    fs = float(np.degrees(np.arctan2(front[0.15] - front[0.30], 0.15))) if front[0.15] is not None and front[0.30] is not None else None
    nose_prot = float(front[0.30]) if front[0.30] is not None else None   # forehead (v 0.30) behind the nose tip, H units: how far the nose projects
    nape = (back[1.00] - depth) if back[1.00] is not None else None
    ns = float(np.degrees(np.arctan2(back[0.90] - back[1.10], 0.20))) if back[0.90] is not None and back[1.10] is not None else None
    nd = (back[1.10] - front[1.10]) if back[1.10] is not None and front[1.10] is not None else None
    tb = (front[1.10] - 0.0) if front[1.10] is not None else None
    # radial profile
    px, py = xn + PIV[0] * H, top + PIV[1] * H; rad = []
    for t in TH:
        a = np.radians(t); dx, dy = np.cos(a), -np.sin(a); r = 0.0
        for k in np.arange(0, 1.5, 0.004):
            x, y = int(round(px + dx * k * H)), int(round(py + dy * k * H))
            if y < 0 or y >= mask.shape[0] or x < 0 or x >= mask.shape[1] or not mask[y, x]:
                if k > 0.1: break
                else: continue
            r = k
        rad.append(float(r))
    rad = np.convolve(np.pad(rad, 2, mode='edge'), np.ones(5) / 5, 'valid').tolist()   # 25 deg boxcar: silhouette noise (stubble, mask edge) is not skull shape
    sc = dict(depth=depth, occ_v=occ_v, crown_frac=crown_u_over(crown_u, depth), crown_u=crown_u, back_flat_hi=bf_hi, back_flat_lo=bf_lo, dome_round=dome, forehead_slope=fs,
              nose_prot=nose_prot, nape_inset=nape, neck_slope=ns, neck_depth=nd, throat_back=tb)
    return dict(H_px=H, anchors=dict(top=top, nose_row=A['nose_row'], nose_x=xn, menton_row=A['menton_row'], pog_row=A['pog_row']), scalars=sc,
                back=[back[v] for v in VG], front=[front[v] for v in VG], radial=rad, nose_v=float(nose_v))


def crown_u_over(crown_u, depth):
    return float(crown_u / depth) if depth else None


def valid_profile(m):
    """pose / measurement sanity for a PHOTO: a clean profile has the nose well ahead of the brow, a head depth in a human window and every scalar defined."""
    if m is None: return False, 'anchors'
    s = m['scalars']
    if any(s[k] is None for k in FEATS): return False, 'undefined'
    if not (0.35 <= m['nose_v'] <= 0.80): return False, 'nose_row'
    if not (0.045 <= s['nose_prot'] <= 0.26): return False, 'nose_not_projecting(3/4 or wrong flip)'
    if not (0.70 <= s['depth'] <= 1.30): return False, 'depth'
    return True, ''


def best_orientation(mask, prefer_flip=False):
    """photo mask -> (measure dict of the LEFT-facing version, flipped?) choosing the orientation where the nose projects (the other one has no nose tip ahead of the brow)."""
    res = []
    for flip in ((prefer_flip, not prefer_flip)):
        mk = mask[:, ::-1] if flip else mask; m = measure(mk); ok, why = valid_profile(m)
        res.append((ok, (m['scalars']['nose_prot'] if m else -9), m, flip, why))
    r = next((x for x in res if x[0]), res[0])   # prompt orientation first, the other one only when it is not a valid profile
    return r[2], r[3], r[0], r[4]


def build(faces, qc=None):
    meta = json.load(open(faces + '/meta.json')); rows = []; rej = {}
    names = sorted(n for n in meta if n.startswith('side_') and os.path.exists(faces + '/' + n))
    thumbs = []
    for n in names:
        img = cv2.imread(faces + '/' + n); mask = segment(img); m, flip, ok, why = best_orientation(mask, meta[n]['facing'] == 'right')
        if not ok: rej[n] = why
        else: rows.append(dict(name=n, flipped=flip, sex=meta[n]['sex'], age=meta[n]['age'], eth=meta[n]['eth'], m=m))
        if qc:
            mk = mask[:, ::-1] if flip else mask; v = img[:, ::-1].copy() if flip else img.copy(); cs, _ = cv2.findContours(mk.astype(np.uint8), cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
            cv2.drawContours(v, cs, -1, (0, 255, 0) if ok else (0, 0, 255), 3)
            if m:
                a = m['anchors']; cv2.circle(v, (int(a['nose_x']), a['nose_row']), 10, (255, 0, 0), -1); cv2.line(v, (0, a['top']), (v.shape[1], a['top']), (255, 255, 0), 2); cv2.line(v, (0, a['menton_row']), (v.shape[1], a['menton_row']), (255, 255, 0), 2)
            cv2.putText(v, n[5:8] + ('' if ok else ' ' + why), (10, 60), cv2.FONT_HERSHEY_SIMPLEX, 2, (0, 0, 255) if not ok else (0, 160, 0), 4); thumbs.append(cv2.resize(v, (256, 256)))
    print('side portraits', len(names), 'valid', len(rows), 'rejected', len(rej), {k: v for k, v in list(rej.items())[:40]})
    if qc and thumbs:
        os.makedirs(qc, exist_ok=True); cols = 10
        for p in range(0, len(thumbs), 50):
            ch = thumbs[p:p + 50]; ch += [np.zeros_like(thumbs[0])] * (-len(ch) % cols)
            cv2.imwrite(qc + '/qc_%d.jpg' % (p // 50), np.vstack([np.hstack(ch[i:i + cols]) for i in range(0, len(ch), cols)]))
    def stat(a):
        a = np.array(a, float); med = float(np.median(a)); mad = float(np.median(np.abs(a - med))); sg = max(1.4826 * mad, 1e-4)
        return dict(median=med, sigma=sg, mean=float(a.mean()), std=float(a.std()), p5=float(np.percentile(a, 5)), p95=float(np.percentile(a, 95)))
    feats = {k: stat([r['m']['scalars'][k] for r in rows]) for k in FEATS}
    def curve(key, n):
        a = np.array([[np.nan if x is None else x for x in r['m'][key]] for r in rows], float); med = np.nanmedian(a, 0); mad = np.nanmedian(np.abs(a - med), 0)
        return dict(median=med.tolist(), sigma=np.maximum(1.4826 * mad, 0.015).tolist(), mean=np.nanmean(a, 0).tolist(), std=np.nanstd(a, 0).tolist(), p5=np.nanpercentile(a, 5, 0).tolist(), p95=np.nanpercentile(a, 95, 0).tolist())
    X = np.array([r['m']['radial'] for r in rows]); mu = X.mean(0); U_, S_, Vt = np.linalg.svd(X - mu, full_matrices=False); K = 6; sd = S_[:K] / np.sqrt(len(X) - 1)
    d_in = np.sqrt((((X - mu) @ Vt[:K].T / sd) ** 2).sum(1))
    pca = dict(k=K, mean=mu.tolist(), comps=Vt[:K].tolist(), std=sd.tolist(), var_explained=(S_[:K] ** 2 / (S_ ** 2).sum()).tolist(), dist_p50=float(np.percentile(d_in, 50)), dist_p90=float(np.percentile(d_in, 90)), dist_p95=float(np.percentile(d_in, 95)), dist_p99=float(np.percentile(d_in, 99)))
    out = dict(pca_radial=pca, n=len(rows), n_generated=len(names), rejected=rej, v_grid=VG, theta_grid=TH, pivot=PIV, features=feats, back=curve('back', len(VG)), front=curve('front', len(VG)), radial=curve('radial', len(TH)),
               per_image={r['name']: dict(flipped=r['flipped'], sex=r['sex'], age=r['age'], eth=r['eth'], scalars=r['m']['scalars']) for r in rows},
               by_sex={s: {k: float(np.median([r['m']['scalars'][k] for r in rows if r['sex'] == s])) for k in FEATS} for s in ('man', 'woman')},
               note='SDXL (RealVisXL V5) bald side portraits, silhouette-only anchors (nose tip, crown, menton). Sigma = 1.4826*MAD (floor 0.015 H on curves: the crown row is the anchor so r(90 deg) is pinned). UNCALIBRATED against measured 3D heads: see SIDE_README.md.')
    json.dump(out, open(PRIOR, 'w'), indent=1); print('wrote', PRIOR)
    for k, f in feats.items(): print('  %-15s median %7.3f sigma %6.3f  p5 %7.3f p95 %7.3f' % (k, f['median'], f['sigma'], f['p5'], f['p95']))


if __name__ == '__main__':
    ap = argparse.ArgumentParser(); ap.add_argument('cmd'); ap.add_argument('--faces', default='C:/AI/test_out/heroes/vision_prior/side'); ap.add_argument('--qc', default=None); a = ap.parse_args()
    if a.cmd == 'build': build(a.faces, a.qc)
