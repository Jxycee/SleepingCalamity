"""Stage 1 prep_image (venv python: cv2 numpy PIL). Config-driven version of the hand-run target/prep.py + prep2.py.
usage: prep_image.py <cfg.json>   -> target/{crop_src.png, mask_raw.png, mask_vis.png, cutout_rgba.png, input_1536.png}
cfg.prep: bg_seed (default true; false = the Cloud hand-tuned behaviour), bg_seed_dist, box [x0,y0,x1,y1] (crop in photo px) or null = auto from the mediapipe face box; ellipses (probable head / sure face / neck), cut_rows, side_cuts, border."""
import os, sys, json
import numpy as np, cv2
from PIL import Image
cfg = json.load(open(sys.argv[1])); P = cfg['prep']; D = os.environ['SCP_RUN'].rstrip('/') + '/target/'
im = np.array(Image.open(cfg['image']).convert('RGB'))[:, :, ::-1].copy()
box = P.get('box'); L = None; AUTO = P.get('auto_mode', 'landmarks')   # auto_mode: 'landmarks' (priors from the mediapipe face oval + ellipses sized by face width/height) | 'fractions' (char2: fixed fractions of the crop)
if box is None or (P.get('ellipses') is None and AUTO == 'landmarks'):   # fallback for a new photo: face box from mediapipe landmarks, grown to a head crop
    import mediapipe as mp
    from mediapipe.tasks import python as mpp
    from mediapipe.tasks.python import vision
    lm = vision.FaceLandmarker.create_from_options(vision.FaceLandmarkerOptions(base_options=mpp.BaseOptions(model_asset_path=os.environ['SCP_FACE_MODEL']), num_faces=1))
    r = lm.detect(mp.Image.create_from_file(cfg['image']))
    if not r.face_landmarks: raise SystemExit('no face found in the photo: give prep.box explicitly')
    L = np.array([[p.x * im.shape[1], p.y * im.shape[0]] for p in r.face_landmarks[0]])
    cx, top, bot = L[:, 0].mean(), L[:, 1].min(), L[:, 1].max(); fh = bot - top
    y0 = top - 0.75 * fh; y1 = bot + 0.55 * fh; w = (y1 - y0) * 0.95
    if box is None:
        box = [int(max(cx - w / 2, 0)), int(max(y0, 0)), int(min(cx + w / 2, im.shape[1])), int(min(y1, im.shape[0]))]; print('auto crop box', box)
X0, Y0, X1, Y1 = box
crop = im[Y0:Y1, X0:X1].copy(); cv2.imwrite(D + 'crop_src.png', crop); h, w = crop.shape[:2]
E = P.get('ellipses')
OVAL = [10, 338, 297, 332, 284, 251, 389, 356, 454, 323, 361, 288, 397, 365, 379, 378, 400, 377, 152, 148, 176, 149, 150, 136, 172, 58, 132, 93, 234, 127, 162, 21, 54, 103, 67, 109]
if E is None and AUTO == 'landmarks' and L is not None:
    # priors from the face landmarks (busy backgrounds: the fixed fractions put background in the head and cut the crown). fw = cheek width, fh = forehead-point to chin.
    fw = np.linalg.norm(L[234] - L[454]); fh = L[152, 1] - L[10, 1]; cx = (L[234, 0] + L[454, 0]) / 2; yc = L[152, 1]
    # cranium: bald-ish skull dome up to ~0.40 fh above the forehead point (bald/shaved; hair is not modelled); probable foreground, centred between the dome top and the ear line
    ytop = L[10, 1] - 0.40 * fh; year = (L[234, 1] + L[454, 1]) / 2
    E = {'head': [cx, (ytop + yc) / 2 + 0.05 * fh, 0.60 * fw, (yc - ytop) / 2 * 1.05], 'face': [cx, L[10, 1] + 0.5 * fh, 0.34 * fw, 0.36 * fh], 'dome': [cx, year, 0.50 * fw, (year - ytop) * 0.95], 'neck': [cx, yc + 0.42 * fh, 0.26 * fw, 0.16 * fh]}
    print('landmark priors fw %.0f fh %.0f' % (fw, fh))
elif E is None:      # relative to the crop: head, face, neck (fractions of w, h)
    E = {'head': [0.5 * w, 0.5 * h, 0.32 * w, 0.42 * h], 'face': [0.5 * w, 0.46 * h, 0.22 * w, 0.32 * h], 'neck': [0.55 * w, 0.88 * h, 0.19 * w, 0.085 * h]}
    E = {k: [v[0] + X0, v[1] + Y0, v[2], v[3]] for k, v in E.items()}     # stored in photo px like the explicit form
mask = np.full((h, w), cv2.GC_PR_BGD, np.uint8)
def e(c, v): cv2.ellipse(mask, (int(c[0] - X0), int(c[1] - Y0)), (int(c[2]), int(c[3])), 0, 0, 360, v, -1)
e(E['head'], cv2.GC_PR_FGD); e(E['face'], cv2.GC_FGD); e(E['neck'], cv2.GC_FGD)
if 'dome' in E and AUTO == 'landmarks':   # super-ellipse (n=2.6, boxier than an ellipse like a real skull), upper half from the ear line: [cx, ear y, half width, height]
    cxd, cyd, ad, bd = E['dome']; tt = np.linspace(0, np.pi, 90); xs = cxd + ad * np.sign(np.cos(tt)) * np.abs(np.cos(tt)) ** (2 / 2.6); ys = cyd - bd * np.abs(np.sin(tt)) ** (2 / 2.6)
    cv2.fillPoly(mask, [np.round(np.c_[xs - X0, ys - Y0]).astype(np.int32)], cv2.GC_FGD)
elif 'dome' in E: e(E['dome'], cv2.GC_FGD)    # landmark mode: inner skull ellipse is sure foreground (the crown is the part GrabCut loses against similar-coloured backgrounds)
if P.get('oval_seed', True) and L is not None and AUTO == 'landmarks' and P.get('ellipses') is None:   # face-oval polygon, shrunk 12 % toward its centre: sure foreground that follows the jaw line
    po = L[OVAL]; po = po.mean(0) + 0.88 * (po - po.mean(0)); cv2.fillPoly(mask, [np.round(po - [X0, Y0]).astype(np.int32)], cv2.GC_FGD)
mask[:int(P.get('border_top', 40)), :] = cv2.GC_BGD
sc0 = P.get('side_cuts')      # photo-px columns outside which is sure background; null = none (the Cloud values only make sense for the Cloud photo)
if sc0:
    sl, sr = sc0; mask[:, :max(int(sl - X0), 0)] = cv2.GC_BGD; mask[:, max(int(sr - X0), 0):] = cv2.GC_BGD
if P.get('bg_seed', True):    # plain backgrounds: pixels close to the border colour (Lab, median of the top/side strips) are sure background (overrides the ellipses: they are wide at the jaw)
    lab = cv2.cvtColor(crop, cv2.COLOR_BGR2LAB).astype(np.float32)
    strips = np.concatenate([lab[:int(0.04 * h)].reshape(-1, 3), lab[:, :int(0.04 * w)].reshape(-1, 3), lab[:, w - int(0.04 * w):].reshape(-1, 3)])
    bgc = np.median(strips, 0); dist = np.linalg.norm(lab - bgc, axis=2)
    sure_fg = (mask == cv2.GC_FGD) if (AUTO == 'landmarks' and 'dome' in E) else np.zeros(mask.shape, bool)    # landmark mode: the sure-foreground shapes are narrower than the old ellipses, so they win over the colour seed (busy backgrounds share colours with the scalp)
    mask[(dist < float(P.get('bg_seed_dist', 10))) & ~sure_fg] = cv2.GC_BGD
    print('bg seed colour (Lab)', bgc.round(1), 'pixels', int((dist < float(P.get('bg_seed_dist', 10))).sum()))
bg = np.zeros((1, 65)); fg = np.zeros((1, 65)); cv2.grabCut(crop, mask, None, bg, fg, 10, cv2.GC_INIT_WITH_MASK)
m = np.where((mask == 1) | (mask == 3), 255, 0).astype(np.uint8)
n, l, s, _ = cv2.connectedComponentsWithStats(m); k = 1 + np.argmax(s[1:, 4]); m = np.where(l == k, 255, 0).astype(np.uint8)
m = cv2.morphologyEx(m, cv2.MORPH_CLOSE, np.ones((9, 9), np.uint8))
if AUTO == 'landmarks' and 'dome' in E and P.get('skull_hull', True):
    # a skull is convex: above the ear line replace the mask by its convex hull (GrabCut loses the crown where the scalp colour matches the background), clipped to the probable-head ellipse
    ye = int(round(E['dome'][1] - Y0 - 0.15 * E['dome'][3])); up = m.copy(); up[ye:] = 0
    cn, _ = cv2.findContours(up, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if cn:
        hull = np.zeros_like(m); cv2.fillPoly(hull, [cv2.convexHull(np.vstack(cn))], 255); lim = np.zeros_like(m)
        cv2.ellipse(lim, (int(E['head'][0] - X0), int(E['head'][1] - Y0)), (int(E['head'][2] * 1.02), int(E['head'][3] * 1.02)), 0, 0, 360, 255, -1); hull[ye:] = 0; m = np.where((hull > 0) & (lim > 0), 255, m).astype(np.uint8)
if AUTO == 'landmarks' and 'dome' in E and P.get('dome_fit', True):
    # round, symmetric crown: the GrabCut / hull mask of a shaved head is skewed and pointed where scalp and background share a colour (char3: +15 px of shelf on one side, 15 px of scalp cut on the other).
    # Robustly fit a symmetric super-ellipse |x-cx|^n/a^n + (yc-y)^n/b^n = 1 (centre column cx = cheek midpoint, free a, b, n in 2..3.6; soft-L1) to the mask contour above the ear line and use it as the mask there.
    from scipy.optimize import least_squares
    cxd, cyd, ad, bd = E['dome']; cxd -= X0; cyd -= Y0; ye2 = int(round(cyd - 0.15 * bd)); up = m.copy(); up[ye2:] = 0
    cn2, _ = cv2.findContours(up, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
    if cn2:
        pts = np.vstack(cn2)[:, 0, :].astype(float); pts = pts[(pts[:, 1] < ye2 - 3) & (pts[:, 1] > 2)]
        def sef(q): a_, b_, n_ = q; u = np.abs(pts[:, 0] - cxd) / a_; v = np.clip(cyd - pts[:, 1], 0, None) / b_; return ((u ** n_ + v ** n_) ** (1 / n_) - 1) * 0.5 * (a_ + b_)
        sol = least_squares(sef, [ad, bd, 2.6], bounds=([0.6 * ad, 0.6 * bd, 2.0], [1.4 * ad, 1.4 * bd, 3.6]), loss='soft_l1', f_scale=4.0)
        a_, b_, n_ = sol.x; tt2 = np.linspace(0, np.pi, 180); xs2 = cxd + a_ * np.sign(np.cos(tt2)) * np.abs(np.cos(tt2)) ** (2 / n_); ys2 = cyd - b_ * np.abs(np.sin(tt2)) ** (2 / n_)
        dm = np.zeros_like(m); cv2.fillPoly(dm, [np.round(np.c_[xs2, ys2]).astype(np.int32)], 255); dm[ye2:] = 0; m[:ye2] = dm[:ye2]
        print('dome fit: a %.0f (prior %.0f) b %.0f (prior %.0f) n %.2f, top y %.0f' % (a_, ad, b_, bd, n_, cyd - b_ + Y0))
cnts, _ = cv2.findContours(m, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE); m = np.zeros_like(m); cv2.drawContours(m, cnts, -1, 255, -1)
m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
cv2.imwrite(D + 'mask_raw.png', m)
vis = crop.copy(); vis[m == 0] = (vis[m == 0] * 0.25 + np.array([0, 0, 160]) * 0.75).astype(np.uint8); cv2.imwrite(D + 'mask_vis.png', vis)
# prep2: cut the chest/shoulders, composite on grey, square pad, 2x Lanczos to 1536
cr = P.get('cut_rows'); cr = cr if cr is not None else [int(0.9 * h), int(0.79 * h), int(0.79 * h)]
m[cr[0]:, :] = 0
sc = P.get('shoulder_cols'); sc = sc if sc is not None else [int(0.29 * w), int(0.81 * w)]
m[cr[1]:, :sc[0]] = 0; m[cr[1]:, sc[1]:] = 0
m = cv2.morphologyEx(m, cv2.MORPH_OPEN, np.ones((7, 7), np.uint8))
n, l, s, _ = cv2.connectedComponentsWithStats(m); k = 1 + np.argmax(s[1:, 4]); m = np.where(l == k, 255, 0).astype(np.uint8); m = cv2.erode(m, np.ones((3, 3), np.uint8))
mf = cv2.GaussianBlur(m, (3, 3), 0).astype(np.float32) / 255
comp = (crop * mf[..., None] + np.array([128, 128, 128], np.float32) * (1 - mf[..., None])).astype(np.uint8)
S = max(h, w) + 60; sq = np.full((S, S, 3), 128, np.uint8); sqa = np.zeros((S, S), np.uint8); oy = (S - h) // 2; ox = (S - w) // 2
sq[oy:oy + h, ox:ox + w] = comp; sqa[oy:oy + h, ox:ox + w] = m
sq = cv2.resize(sq, (1536, 1536), interpolation=cv2.INTER_LANCZOS4); sqa = cv2.resize(sqa, (1536, 1536), interpolation=cv2.INTER_LANCZOS4)
cv2.imwrite(D + 'cutout_rgba.png', np.dstack([sq, sqa])); cv2.imwrite(D + 'input_1536.png', sq)
print('prep done', S, ox, oy, h, w)
