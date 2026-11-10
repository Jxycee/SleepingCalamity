"""Shared helpers for the multi-view head tools (segmentation, head-frame canonical rasters, IoU, side-prior scoring). Imports the single-photo vision tools unchanged."""
import os, sys, json, numpy as np, cv2
HERE = os.path.dirname(os.path.abspath(__file__)).replace('\\', '/')
VIS = os.path.abspath(os.path.join(HERE, '..', 'vision')).replace('\\', '/')
sys.path.insert(0, VIS)
import side_prior as SP, side_profile_metric as SPM, vcommon as VC

VIEWS = ['front', 'q34l', 'left', 'right', 'q34r', 'back']


def seg_png(path, thr=9.0, work=512, skip_top=0.0, cut=0.48):
    """Like side_prior.segment but the background fit uses only the top strip and the side strips of the top 40 % rows (turnaround tiles have the shirt at the lower side edges)."""
    from scipy import ndimage as ndi
    bgr = cv2.imread(path); h, w = bgr.shape[:2]; k0 = int(skip_top * h); bgr = bgr[k0:]; h = h - k0; s = work / max(h, w); sm = cv2.resize(bgr, (int(w * s), int(h * s)), interpolation=cv2.INTER_AREA); H, W = sm.shape[:2]
    lab = cv2.cvtColor(cv2.GaussianBlur(sm, (0, 0), 1.5), cv2.COLOR_BGR2LAB).astype(np.float32); yy, xx = np.mgrid[0:H, 0:W]; xn, yn = xx / W - 0.5, yy / H - 0.5
    A = np.stack([np.ones_like(xn), xn, yn, xn * xn, xn * yn, yn * yn], -1).reshape(-1, 6); Lc = lab.reshape(-1, 3)
    sel = np.zeros((H, W), bool); sel[:int(0.06 * H)] = True; sel[:int(0.40 * H), :int(0.04 * W)] = True; sel[:int(0.40 * H), W - int(0.04 * W):] = True; sel = sel.ravel(); use = sel.copy()
    for _ in range(3):
        coef = np.linalg.lstsq(A[use], Lc[use], rcond=None)[0]; d = np.linalg.norm(A @ coef - Lc, axis=1); use = sel & (d < max(2.0 * np.median(d[sel]), 4.0))
    fg = np.linalg.norm(A @ coef - Lc, axis=1).reshape(H, W) > thr
    fg = ndi.binary_closing(fg, iterations=4); fg = ndi.binary_opening(fg, iterations=2); fg = ndi.binary_fill_holes(fg); lab_, n = ndi.label(fg)
    if n == 0: return np.zeros((h, w), bool)
    fg = lab_ == (1 + int(np.argmax(ndi.sum(fg, lab_, range(1, n + 1)))))
    out = cv2.resize(fg.astype(np.uint8), (w, h), interpolation=cv2.INTER_NEAREST).astype(bool)
    rows = np.where(out.any(1))[0]
    if len(rows) and cut: out[int(rows[0] + cut * w):] = False       # drop shoulders / shirt below ~1.15 H under the crown (they merge with the neck and break the menton anchor)
    full = np.zeros((h + k0, w), bool); full[k0:] = out; return full


def seg_view(path_prefix_dir, tag, view):
    return seg_png(f'{path_prefix_dir}/{tag}_{view}.png', skip_top=0.07 if view in ('right', 'q34r', 'back') else 0.0)


def canon(mask, H, res=256):
    """Head-frame raster: crown row aligned, scale 1/H (crown-to-menton), horizontally centred on the skull mid-line of rows 0.05-0.45 H. v 0..0.95, u -0.9..0.9."""
    rows = np.where(mask.any(1))[0]; top = int(rows[0]); mids = []
    for v in np.linspace(0.05, 0.45, 12):
        r = int(round(top + v * H)); xs = np.where(mask[min(r, mask.shape[0] - 1)])[0]
        if len(xs): mids.append(0.5 * (xs.min() + xs.max()))
    cx = float(np.mean(mids)); v = np.linspace(0, 0.95, res); u = np.linspace(-0.9, 0.9, 2 * res)
    ys = np.clip(np.round(top + v * H).astype(int), 0, mask.shape[0] - 1); xs = np.clip(np.round(cx + u * H).astype(int), 0, mask.shape[1] - 1)
    return mask[np.ix_(ys, xs)]


def iou(a, b):
    return float((a & b).sum() / max((a | b).sum(), 1))


def prior():
    return json.load(open(SP.PRIOR))


def score_profile(name, mask_left_facing, P):
    """side-prior z report for a left-facing silhouette of unknown scale: mm are NOMINAL (H assumed 225 mm)."""
    m = SP.measure(mask_left_facing)
    if m is None: return None, None, None
    VC.S2, VC.R2 = 0.225, m['H_px']                 # report() converts H_px to mm with S2 / R2: makes 1 H = 225 mm
    lines, js = SPM.report(name, m, P)
    return m, lines, js
