# Numeric scores for one sweep variant (trellis venv python: numpy, scipy, cv2).
#   python sweep_score.py <variant_dir> <ctx.json>      -> <variant_dir>/scores.json
# ctx.json: head (head.npz), hull (hull dir with masks + hull_cal.json), refs (dir with crop_front/side/back.png), jut_mm, zmin.
# Inputs in the variant dir: opt/best.json + opt/fit_best.npz (hd_opt), prev_*.png + hair_*.png (sweep_render.py), fit.log (hd_fit.py stdout).
# Scores:
#   iou_front/side/back/frame, iou_mean, over_*, miss_*     from hd_opt's best fit (hd_sil.py)
#   jut_<view>_frac / jut_<view>_max_mm   hair verts (z > 1.60) whose projection lies > jut_mm outside the reference silhouette
#                                         (jut_side = cards sticking out in the side view)
#   dE_<view>, dL_<view>, hue_diff_back, chroma_ratio_back, warm_back   Lab distance of the rendered hair region to the reference hair
#                                         region (reference mask above zmin); warm_back=1 when the back reads more orange / more saturated
#   clear_pushed_frac, clear_worst_mm, cover, tris, crown_cm, ear_cover   hd_fit CLEAR/COVER lines, hair top vs skull top, share of
#                                         ear verts with hair outside them (approximation of the builder's EARS ray test)
import sys, os, json, re, numpy as np, cv2
from scipy.spatial import cKDTree
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'hairdeck'))
from hd_sil import load_cal, raster, load_mesh, AX   # noqa: E402
vd, ctxp = sys.argv[1:3]; ctx = json.load(open(ctxp)); ZMIN = ctx.get('zmin', 1.68); JUT = ctx.get('jut_mm', 6.0)
cal, M = load_cal(ctx['hull']); R = {}
best = json.load(open(os.path.join(vd, 'opt', 'best.json')))['best']
for v in ('front', 'side', 'back', 'frame'):
    for k in ('iou', 'over', 'miss'): R[f'{k}_{v}'] = best['sil'][v][k]
R['iou_mean'] = float(np.mean([R[f'iou_{v}'] for v in ('front', 'side', 'back')]))
fit = np.load(os.path.join(vd, 'opt', 'fit_best.npz')); HV, HF = fit['v'].astype(float), fit['f']
R['tris'] = int(len(HF)); R['verts'] = int(len(HV))
# jutting
sel = HV[HV[:, 2] > 1.60]
for v in AX:
    c = cal[v]; s = c['s']; sz = c.get('sz', s); ref = M[v]
    px = c['cx'] + c['sign'] * s * sel[:, AX[v]]; py = c['eye_py'] - sz * (sel[:, 2] - cal['eye_z'])
    dt = cv2.distanceTransform((~ref).astype(np.uint8), cv2.DIST_L2, 5)
    h, w = ref.shape; xi = np.clip(np.round(px).astype(int), 0, w - 1); yi = np.clip(np.round(py).astype(int), 0, h - 1)
    d = dt[yi, xi] + np.abs(px - xi) + np.abs(py - yi)            # px outside the image count as farther out
    d = np.where(ref[yi, xi] & (np.abs(px - xi) < 1) & (np.abs(py - yi) < 1), 0, d)
    mm = d / min(s, sz) * 1000
    R[f'jut_{v}_frac'] = float((mm > JUT).mean()); R[f'jut_{v}_max_mm'] = float(mm.max())
# colour
def lab(img_rgb): return cv2.cvtColor(img_rgb.astype(np.float32) / 255, cv2.COLOR_RGB2LAB)
def hairpix(path, region):
    im = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    if im is None: return None
    rgb = cv2.cvtColor(im[:, :, :3], cv2.COLOR_BGR2RGB); L = lab(rgb)
    sel_ = region(im)
    return L[sel_] if sel_.sum() > 50 else None
def ref_region(view):
    c = cal[view]; sz = c.get('sz', c['s']); row = int(c['eye_py'] - sz * (ZMIN - cal['eye_z'])); m = M[view].copy(); m[max(row, 0):] = False; return m
def ren_region(im):
    a = im[:, :, 3] > 127; a[442 * im.shape[0] // 700:] = False; return a        # ortho 0.42 m around z 1.735: z 1.68 sits at row 442 of 700
hue = lambda m: float(np.degrees(np.arctan2(m[2], m[1]))); chroma = lambda m: float(np.hypot(m[1], m[2]))
for v in ('front', 'side', 'back'):
    rp = hairpix(os.path.join(ctx['refs'], f'crop_{v}.png'), lambda im, v=v: ref_region(v)); hp = hairpix(os.path.join(vd, f'hair_{v}.png'), ren_region)
    if rp is None or hp is None: R[f'dE_{v}'] = R[f'dL_{v}'] = None; continue
    mr, mh = rp.mean(0), hp.mean(0)
    R[f'dL_{v}'] = float(mh[0] - mr[0]); R[f'dE_{v}'] = float(np.sqrt((0.5 * (mh[0] - mr[0])) ** 2 + (mh[1] - mr[1]) ** 2 + (mh[2] - mr[2]) ** 2))
    R[f'L_p50_diff_{v}'] = float(np.median(hp[:, 0]) - np.median(rp[:, 0]))
    if v == 'back':
        R['hue_diff_back'] = hue(mh) - hue(mr); R['chroma_ratio_back'] = chroma(mh) / max(chroma(mr), 1e-3)
        R['warm_back'] = int(R['hue_diff_back'] > 8 or R['chroma_ratio_back'] > 1.4)
R['dE_mean'] = float(np.mean([R[f'dE_{v}'] for v in ('front', 'side', 'back') if R.get(f'dE_{v}') is not None] or [99]))
# outline roughness: perimeter / convex-hull perimeter of the largest blob after a 5 px open (smooth bowl ~1.0-1.1, spiky more)
def rough(mask):
    m = cv2.morphologyEx(mask.astype(np.uint8) * 255, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
    cs, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if not cs: return None
    c = max(cs, key=cv2.contourArea); hp = cv2.arcLength(cv2.convexHull(c), True)
    return float(cv2.arcLength(c, True) / hp) if hp > 0 else None
for v in ('front', 'side', 'back'):
    im = cv2.imread(os.path.join(vd, f'hair_{v}.png'), cv2.IMREAD_UNCHANGED)
    R[f'rough_{v}'] = rough(ren_region(im)) if im is not None else None
    R[f'rough_ref_{v}'] = rough(ref_region(v))
# hd_fit log: CLEAR pushed N of M verts, worst before W m ; COVER a/b
log = open(os.path.join(vd, 'fit.log'), errors='ignore').read()
m = re.search(r'CLEAR pushed (\d+) of (\d+) verts, worst before (-?[\d.]+)', log)
if m: R['clear_pushed_frac'] = int(m[1]) / max(int(m[2]), 1); R['clear_worst_mm'] = float(m[3]) * 1000
m = re.search(r'COVER (\d+)/(\d+)', log)
if m: R['cover'] = int(m[1]) / max(int(m[2]), 1)
# crown + ears against the head
hv, hf = load_mesh(ctx['head'], 'AvatarHead')
R['crown_cm'] = float((HV[:, 2].max() - hv[:, 2].max()) * 100)
ear = hv[(np.abs(hv[:, 0]) > 0.068) & (hv[:, 2] > 1.70) & (hv[:, 2] < 1.765)]
if len(ear):
    tri = HV[HF]; pts = np.vstack([HV, tri.mean(1), (tri[:, 0] + tri[:, 1]) / 2, (tri[:, 1] + tri[:, 2]) / 2])
    tree = cKDTree(pts[:, 1:]); cov = 0
    for p in ear:
        idx = tree.query_ball_point(p[1:], 0.008)
        cov += any(abs(pts[i, 0]) > abs(p[0]) - 0.002 and np.sign(pts[i, 0]) == np.sign(p[0]) for i in idx)
    R['ear_cover'] = cov / len(ear)
R['params'] = best['params']
json.dump(R, open(os.path.join(vd, 'scores.json'), 'w'), indent=1)
print('SCORED', os.path.basename(vd), {k: round(v, 3) for k, v in R.items() if isinstance(v, float) and k in ('iou_mean', 'iou_frame', 'dE_mean', 'jut_side_frac')})
